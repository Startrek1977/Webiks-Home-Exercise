using System;
using System.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RoverRally.Core.Logging;
using RoverRally.Core.Units;

namespace RoverRally.Core.Configuration
{
    /// <summary>
    /// Station configuration. Site-wide values (ports, track extent, roster
    /// location) come from the application configuration file. Anything an
    /// operator can change from the UI is kept per operator in the registry,
    /// because the station PCs are shared between shifts.
    /// </summary>
    public static class StationSettings
    {
        private const string RegistryPath = @"Software\RoverLink\Station";

        public static int TelemetryPort
        {
            get { return ReadInt("TelemetryPort", 14550); }
        }

        public static int CommandPort
        {
            get { return ReadInt("CommandPort", 14551); }
        }

        public static string RosterPath
        {
            get { return ReadString("RosterPath", @"Data\rovers.json"); }
        }

        public static string SessionCachePath
        {
            get { return ReadString("SessionCachePath", @"Data\session-cache.bin"); }
        }

        public static double TrackNorth { get { return ReadDouble("TrackNorth", 32.2830); } }
        public static double TrackSouth { get { return ReadDouble("TrackSouth", 32.2770); } }
        public static double TrackWest { get { return ReadDouble("TrackWest", 34.9160); } }
        public static double TrackEast { get { return ReadDouble("TrackEast", 34.9250); } }

        /// <summary>
        /// Each operator gets their own slot under the station key so that two
        /// shifts sharing a PC do not overwrite each other's preferences - as
        /// long as each shift actually logs into its own Windows account.
        /// <c>HKCU</c> is already scoped to one Windows user, so this split
        /// only separates operators who share a PC but not a login; it does
        /// nothing for operators who share both, since they'd compute the
        /// same <see cref="Environment.UserName"/> and land in the same slot.
        /// </summary>
        /// <remarks>
        /// Previously <c>"Profile_" + Environment.UserName.GetHashCode().ToString("X8")</c>.
        /// <see cref="string.GetHashCode()"/> is randomized per process on
        /// .NET Core and later (it was stable on .NET Framework), so that
        /// derivation produced a different key on every launch once this
        /// station moved off net48 - preferences appeared to reset every time,
        /// and the abandoned keys never got cleaned up. Deriving from the
        /// sanitized username itself instead of any hash is deterministic
        /// across launches by construction, and keeps the key human-readable
        /// in the registry.
        /// </remarks>
        private static string ProfileKey
        {
            get { return "Profile_" + BuildProfileKeyName(Environment.UserName); }
        }

        private static readonly Regex UnsafeProfileKeyCharacters = new Regex(@"[^A-Za-z0-9._-]", RegexOptions.Compiled);

        /// <summary>
        /// Turns a Windows username into a stable, registry-legal subkey
        /// fragment: any character outside <c>[A-Za-z0-9._-]</c> - including
        /// the backslash that would otherwise be read as a path separator -
        /// is replaced with <c>_</c>, one for one, so a non-empty username
        /// always sanitizes to a non-empty result. A <c>null</c>/empty
        /// username falls back to a fixed placeholder instead, since an empty
        /// subkey name passed to <see cref="RegistryKey.CreateSubKey(string)"/>
        /// would target the parent key itself instead of a real child key.
        /// </summary>
        public static string BuildProfileKeyName(string userName)
        {
            return string.IsNullOrEmpty(userName) ? "unknown" : UnsafeProfileKeyCharacters.Replace(userName, "_");
        }

        /// <summary>
        /// One-time sweep for <c>Profile_*</c> keys left behind by the old
        /// per-process-randomized-hash derivation: every launch used to mint
        /// a new one that was never read again. Safe to run unconditionally
        /// because <c>HKCU</c> is already scoped to the current Windows user,
        /// so within this hive every <c>Profile_*</c> sibling other than
        /// <see cref="ProfileKey"/> is guaranteed to be that kind of orphan,
        /// not another operator's live data - the OS guarantees this station
        /// only ever sees one Windows account's registry keys here.
        /// </summary>
        public static void CleanUpAbandonedProfileKeys()
        {
            try
            {
                string currentProfileKey = ProfileKey;
                using (RegistryKey stationKey = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true))
                {
                    if (stationKey == null) return;

                    foreach (string subKeyName in stationKey.GetSubKeyNames())
                    {
                        bool isAbandonedProfile = subKeyName.StartsWith("Profile_", StringComparison.Ordinal)
                            && !string.Equals(subKeyName, currentProfileKey, StringComparison.Ordinal);

                        if (isAbandonedProfile)
                        {
                            stationKey.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not clean up abandoned operator profile keys: " + ex.Message);
            }
        }

        public static SpeedUnit PreferredSpeedUnit
        {
            get
            {
                object stored = ReadProfileValue("SpeedUnit");
                if (stored == null) return SpeedUnit.KilometresPerHour;
                return string.Equals(stored.ToString(), "mph", StringComparison.OrdinalIgnoreCase)
                    ? SpeedUnit.MilesPerHour
                    : SpeedUnit.KilometresPerHour;
            }
            set
            {
                WriteProfileValue("SpeedUnit", value == SpeedUnit.MilesPerHour ? "mph" : "kmh");
            }
        }

        public static int LastSelectedRoverId
        {
            get
            {
                object stored = ReadProfileValue("LastSelectedRoverId");
                if (stored == null) return 0;
                int parsed;
                return int.TryParse(stored.ToString(), out parsed) ? parsed : 0;
            }
            set { WriteProfileValue("LastSelectedRoverId", value.ToString(CultureInfo.InvariantCulture)); }
        }

        private static object ReadProfileValue(string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath + "\\" + ProfileKey))
                {
                    return key == null ? null : key.GetValue(name);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read operator profile: " + ex.Message);
                return null;
            }
        }

        private static void WriteProfileValue(string name, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath + "\\" + ProfileKey))
                {
                    if (key != null) key.SetValue(name, value);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save operator profile: " + ex.Message);
            }
        }

        private static string ReadString(string name, string fallback)
        {
            string raw = ConfigurationManager.AppSettings[name];
            return string.IsNullOrEmpty(raw) ? fallback : raw;
        }

        private static int ReadInt(string name, int fallback)
        {
            int parsed;
            string raw = ConfigurationManager.AppSettings[name];
            return int.TryParse(raw, out parsed) ? parsed : fallback;
        }

        private static double ReadDouble(string name, double fallback)
        {
            double parsed;
            string raw = ConfigurationManager.AppSettings[name];
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
    }
}
