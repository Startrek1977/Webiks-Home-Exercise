using System;
using System.Configuration;
using System.Globalization;
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
        /// shifts sharing a PC do not overwrite each other's preferences.
        /// </summary>
        private static string ProfileKey
        {
            get { return "Profile_" + Environment.UserName.GetHashCode().ToString("X8"); }
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
