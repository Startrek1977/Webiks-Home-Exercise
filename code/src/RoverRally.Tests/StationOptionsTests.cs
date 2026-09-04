using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Configuration;
using RoverRally.Core.Logging;

namespace RoverRally.Tests
{
    [TestClass]
    public class StationOptionsTests
    {
        private static IConfiguration BuildConfiguration(IDictionary<string, string> stationValues)
        {
            var flattened = new Dictionary<string, string>();
            if (stationValues != null)
            {
                foreach (KeyValuePair<string, string> pair in stationValues)
                {
                    flattened["Station:" + pair.Key] = pair.Value;
                }
            }

            return new ConfigurationBuilder().AddInMemoryCollection(flattened).Build();
        }

        [TestMethod]
        public void LoadUsesTheDocumentedDefaultsWhenTheStationSectionIsEntirelyAbsent()
        {
            StationOptions options = StationOptions.Load(BuildConfiguration(null));

            Assert.AreEqual("RoverRally Station", options.StationName);
            Assert.AreEqual(14550, options.TelemetryPort);
            Assert.AreEqual(14551, options.CommandPort);
            Assert.AreEqual("127.0.0.1", options.CommandHost);
            Assert.AreEqual(@"Data\rovers.json", options.RosterPath);
            Assert.AreEqual(@"Data\session-cache.bin", options.SessionCachePath);
            Assert.AreEqual(32.2830, options.TrackNorth);
            Assert.AreEqual(32.2770, options.TrackSouth);
            Assert.AreEqual(34.9160, options.TrackWest);
            Assert.AreEqual(34.9250, options.TrackEast);
            Assert.AreEqual(200, options.DriveCommandIntervalMs);
            Assert.AreEqual(LogLevel.Info, options.LogLevel);
            Assert.AreEqual(
                System.Environment.ExpandEnvironmentVariables(@"%LocalAppData%\RoverLink\Station\Logs"),
                options.LogDirectory);
        }

        [TestMethod]
        public void LoadHonoursEveryKeyThatIsPresentAndValid()
        {
            var values = new Dictionary<string, string>
            {
                ["StationName"] = "Test Track",
                ["TelemetryPort"] = "15550",
                ["CommandPort"] = "15551",
                ["CommandHost"] = "192.168.1.50",
                ["RosterPath"] = @"Config\other-rovers.json",
                ["SessionCachePath"] = @"Config\other-cache.bin",
                ["TrackNorth"] = "40.1",
                ["TrackSouth"] = "40.0",
                ["TrackWest"] = "-74.1",
                ["TrackEast"] = "-74.0",
                ["DriveCommandIntervalMs"] = "100",
                ["LogLevel"] = "Warn",
                ["LogDirectory"] = @"D:\RoverLogs"
            };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual("Test Track", options.StationName);
            Assert.AreEqual(15550, options.TelemetryPort);
            Assert.AreEqual(15551, options.CommandPort);
            Assert.AreEqual("192.168.1.50", options.CommandHost);
            Assert.AreEqual(@"Config\other-rovers.json", options.RosterPath);
            Assert.AreEqual(@"Config\other-cache.bin", options.SessionCachePath);
            Assert.AreEqual(40.1, options.TrackNorth);
            Assert.AreEqual(40.0, options.TrackSouth);
            Assert.AreEqual(-74.1, options.TrackWest);
            Assert.AreEqual(-74.0, options.TrackEast);
            Assert.AreEqual(100, options.DriveCommandIntervalMs);
            Assert.AreEqual(LogLevel.Warn, options.LogLevel);
            Assert.AreEqual(@"D:\RoverLogs", options.LogDirectory);
        }

        [TestMethod]
        public void LoadExpandsEnvironmentVariablesInLogDirectory()
        {
            var values = new Dictionary<string, string> { ["LogDirectory"] = @"%TEMP%\RoverLink\Logs" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(System.Environment.ExpandEnvironmentVariables(@"%TEMP%\RoverLink\Logs"), options.LogDirectory);
            StringAssert.DoesNotMatch(options.LogDirectory, new System.Text.RegularExpressions.Regex("%"));
        }

        /// <summary>
        /// A relative value on its own would leave the log path dependent on
        /// whatever the process's current directory happens to be at the
        /// point Serilog opens the file - not necessarily the station's own
        /// install directory. Rooting it here, the same way RosterPath and
        /// SessionCachePath are rooted by their caller, keeps it predictable
        /// regardless of how the station was launched (#22 review).
        /// </summary>
        [TestMethod]
        public void LoadRootsARelativeLogDirectoryAgainstTheApplicationBaseDirectory()
        {
            var values = new Dictionary<string, string> { ["LogDirectory"] = @"Logs\Custom" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            string expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"Logs\Custom"));
            Assert.AreEqual(expected, options.LogDirectory);
            Assert.IsTrue(System.IO.Path.IsPathRooted(options.LogDirectory));
        }

        [TestMethod]
        public void LoadFallsBackToTheDefaultWhenLogDirectoryIsWhitespace()
        {
            var values = new Dictionary<string, string> { ["LogDirectory"] = "   " };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(
                System.Environment.ExpandEnvironmentVariables(@"%LocalAppData%\RoverLink\Station\Logs"),
                options.LogDirectory);
        }

        [TestMethod]
        public void LoadFallsBackToTheDefaultWhenAnIntegerKeyIsPresentButMalformed()
        {
            var values = new Dictionary<string, string> { ["TelemetryPort"] = "not-a-port" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(14550, options.TelemetryPort);
        }

        [TestMethod]
        public void LoadFallsBackToTheDefaultWhenADoubleKeyIsPresentButMalformed()
        {
            var values = new Dictionary<string, string> { ["TrackNorth"] = "not-a-coordinate" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(32.2830, options.TrackNorth);
        }

        [TestMethod]
        public void LoadFallsBackToTheDefaultWhenLogLevelIsPresentButNotARecognisedLevel()
        {
            var values = new Dictionary<string, string> { ["LogLevel"] = "Verbose" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(LogLevel.Info, options.LogLevel);
        }

        /// <summary>
        /// Enum.TryParse accepts any numeric string as a value of the target
        /// enum even when nothing declares that value - "99" parses to a real
        /// but undefined LogLevel rather than failing, which would then compare
        /// greater than every real level and silently suppress all logging.
        /// </summary>
        [TestMethod]
        public void LoadFallsBackToTheDefaultWhenLogLevelIsAnUndefinedNumericValue()
        {
            var values = new Dictionary<string, string> { ["LogLevel"] = "99" };

            StationOptions options = StationOptions.Load(BuildConfiguration(values));

            Assert.AreEqual(LogLevel.Info, options.LogLevel);
        }

        [TestMethod]
        public void LoadParsesTrackCoordinatesWithInvariantCultureRegardlessOfTheCurrentThreadCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                // A culture that uses ',' as the decimal separator would silently
                // misparse "32.5" as 325 under CurrentCulture rules.
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

                var values = new Dictionary<string, string> { ["TrackNorth"] = "32.5" };
                StationOptions options = StationOptions.Load(BuildConfiguration(values));

                Assert.AreEqual(32.5, options.TrackNorth);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
