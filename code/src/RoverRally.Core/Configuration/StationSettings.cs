using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RoverRally.Core.Logging;
using RoverRally.Core.Units;

namespace RoverRally.Core.Configuration
{
    /// <summary>
    /// Station configuration. Site-wide values (ports, track extent, roster
    /// location) come from <c>appsettings.json</c>, bound once at startup via
    /// <see cref="Configure"/> into a <see cref="StationOptions"/>. Anything
    /// an operator can change from the UI is kept per operator in the
    /// registry, because the station PCs are shared between shifts.
    /// </summary>
    public static class StationSettings
    {
        private const string RegistryPath = @"Software\RoverLink\Station";

        private static StationOptions _options;

        /// <summary>
        /// Supplies the bound site configuration. Must be called once, before
        /// any of the config-backed properties below are read - the app does
        /// this first thing in <c>OnStartup</c>, ahead of anything that could
        /// read a property.
        /// </summary>
        public static void Configure(StationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private static StationOptions Options
        {
            get
            {
                if (_options == null)
                {
                    throw new InvalidOperationException(
                        "StationSettings.Configure must be called before reading station configuration.");
                }

                return _options;
            }
        }

        public static string StationName { get { return Options.StationName; } }
        public static int TelemetryPort { get { return Options.TelemetryPort; } }
        public static int CommandPort { get { return Options.CommandPort; } }
        public static string CommandHost { get { return Options.CommandHost; } }
        public static string RosterPath { get { return Options.RosterPath; } }
        public static string SessionCachePath { get { return Options.SessionCachePath; } }
        public static double TrackNorth { get { return Options.TrackNorth; } }
        public static double TrackSouth { get { return Options.TrackSouth; } }
        public static double TrackWest { get { return Options.TrackWest; } }
        public static double TrackEast { get { return Options.TrackEast; } }
        public static int DriveCommandIntervalMs { get { return Options.DriveCommandIntervalMs; } }
        public static Logging.LogLevel LogLevel { get { return Options.LogLevel; } }

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
        /// per-process-randomized-hash derivation. Safe to run unconditionally
        /// because <c>HKCU</c> is already scoped to the current Windows user,
        /// so within this hive every <c>Profile_*</c> sibling other than
        /// <see cref="ProfileKey"/> belongs to this same operator, not another
        /// one - the OS guarantees this station only ever sees one Windows
        /// account's registry keys here.
        /// </summary>
        /// <remarks>
        /// A sibling isn't necessarily worthless garbage: a station upgrading
        /// straight from the old net48 build carries a <c>Profile_&lt;hash&gt;</c>
        /// key whose hash was stable there and whose values are the operator's
        /// real, live preferences - only per-process randomization on .NET
        /// Core/.NET 8 made the *later* siblings write-once orphans. Each
        /// sibling's values are migrated into the current key - without
        /// overwriting anything already there - before it is deleted, so
        /// neither case loses data: a genuine legacy profile survives the
        /// upgrade, and an orphan with nothing salvageable is deleted exactly
        /// as before.
        /// </remarks>
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
                            MigrateProfileValues(stationKey, subKeyName, currentProfileKey);
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

        private static readonly string[] ProfileValueNames = { "SpeedUnit", "LastSelectedRoverId" };

        /// <summary>
        /// Copies any of <see cref="ProfileValueNames"/> present under
        /// <paramref name="fromSubKeyName"/> into <paramref name="toSubKeyName"/>,
        /// skipping any name the destination already has - the destination is
        /// only ever created if there is actually something to carry over.
        /// </summary>
        private static void MigrateProfileValues(RegistryKey stationKey, string fromSubKeyName, string toSubKeyName)
        {
            using (RegistryKey fromKey = stationKey.OpenSubKey(fromSubKeyName))
            {
                if (fromKey == null) return;

                RegistryKey toKey = null;
                try
                {
                    foreach (string valueName in ProfileValueNames)
                    {
                        object value = fromKey.GetValue(valueName);
                        if (value == null) continue;

                        toKey = toKey ?? stationKey.CreateSubKey(toSubKeyName);
                        if (toKey != null && toKey.GetValue(valueName) == null)
                        {
                            toKey.SetValue(valueName, value);
                        }
                    }
                }
                finally
                {
                    if (toKey != null) toKey.Dispose();
                }
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
    }
}
