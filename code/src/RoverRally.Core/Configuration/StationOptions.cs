using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Configuration;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Configuration
{
    /// <summary>
    /// Strongly-typed site configuration bound from the <c>"Station"</c>
    /// section of <c>appsettings.json</c>. A key that is absent keeps the
    /// documented default silently - that is the intended behaviour when a
    /// site simply hasn't overridden it. A key that is present but cannot be
    /// parsed also falls back to the same default, but logs an error first,
    /// so a typo in the file is visible instead of quietly reverting to a
    /// default nobody chose.
    /// </summary>
    public sealed class StationOptions
    {
        public string StationName { get; set; }
        public int TelemetryPort { get; set; }
        public int CommandPort { get; set; }
        public string CommandHost { get; set; }
        public string RosterPath { get; set; }
        public string SessionCachePath { get; set; }
        public double TrackNorth { get; set; }
        public double TrackSouth { get; set; }
        public double TrackWest { get; set; }
        public double TrackEast { get; set; }
        public int DriveCommandIntervalMs { get; set; }
        public LogLevel LogLevel { get; set; }
        public string LogDirectory { get; set; }

        /// <summary>
        /// Per-user, non-admin-writable, and outside the exe's own install
        /// location - the station may be installed under Program Files,
        /// which a non-admin operator cannot write to (#22).
        /// </summary>
        private const string DefaultLogDirectory = @"%LocalAppData%\RoverLink\Station\Logs";

        public static StationOptions Load(IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            IConfigurationSection section = configuration.GetSection("Station");

            return new StationOptions
            {
                StationName = ReadString(section, "StationName", "RoverRally Station"),
                TelemetryPort = ReadInt(section, "TelemetryPort", 14550),
                CommandPort = ReadInt(section, "CommandPort", 14551),
                CommandHost = ReadString(section, "CommandHost", "127.0.0.1"),
                RosterPath = ReadString(section, "RosterPath", @"Data\rovers.json"),
                SessionCachePath = ReadString(section, "SessionCachePath", @"Data\session-cache.bin"),
                TrackNorth = ReadDouble(section, "TrackNorth", 32.2830),
                TrackSouth = ReadDouble(section, "TrackSouth", 32.2770),
                TrackWest = ReadDouble(section, "TrackWest", 34.9160),
                TrackEast = ReadDouble(section, "TrackEast", 34.9250),
                DriveCommandIntervalMs = ReadInt(section, "DriveCommandIntervalMs", 200),
                LogLevel = ReadLogLevel(section, "LogLevel", LogLevel.Info),
                LogDirectory = ReadLogDirectory(section, "LogDirectory", DefaultLogDirectory)
            };
        }

        private static string ReadString(IConfigurationSection section, string key, string fallback)
        {
            string raw = section[key];
            return string.IsNullOrEmpty(raw) ? fallback : raw;
        }

        private static int ReadInt(IConfigurationSection section, string key, int fallback)
        {
            string raw = section[key];
            if (string.IsNullOrEmpty(raw)) return fallback;

            int parsed;
            if (int.TryParse(raw, out parsed)) return parsed;

            Log.Error("Station:" + key + " has an invalid value \"" + raw + "\"; using default " + fallback + ".");
            return fallback;
        }

        private static double ReadDouble(IConfigurationSection section, string key, double fallback)
        {
            string raw = section[key];
            if (string.IsNullOrEmpty(raw)) return fallback;

            double parsed;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;

            Log.Error("Station:" + key + " has an invalid value \"" + raw + "\"; using default " + fallback + ".");
            return fallback;
        }

        /// <summary>
        /// Unlike the other ReadX helpers, any non-blank string is a
        /// syntactically "valid" directory, so there is no malformed case to
        /// log and fall back from - only absent or whitespace-only, which
        /// use <paramref name="fallback"/>. Both the configured value and
        /// the fallback are expanded (<c>%LocalAppData%</c> and friends),
        /// trimmed, and rooted against the application's base directory if
        /// not already absolute, so callers always receive a real, absolute
        /// path - not just an expanded one, which a relative value like
        /// <c>"Logs"</c> would otherwise still be.
        /// </summary>
        private static string ReadLogDirectory(IConfigurationSection section, string key, string fallback)
        {
            string raw = section[key];
            string path = string.IsNullOrWhiteSpace(raw) ? fallback : raw;
            string expanded = Environment.ExpandEnvironmentVariables(path).Trim();

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expanded));
        }

        private static LogLevel ReadLogLevel(IConfigurationSection section, string key, LogLevel fallback)
        {
            string raw = section[key];
            if (string.IsNullOrEmpty(raw)) return fallback;

            LogLevel parsed;
            if (Enum.TryParse(raw, true, out parsed) && Enum.IsDefined(typeof(LogLevel), parsed)) return parsed;

            Log.Error("Station:" + key + " has an invalid value \"" + raw + "\"; using default " + fallback + ".");
            return fallback;
        }
    }
}
