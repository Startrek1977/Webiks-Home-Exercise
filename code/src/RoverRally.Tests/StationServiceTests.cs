using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.Services;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    [TestClass]
    public class StationServiceTests
    {
        [TestInitialize]
        public void Setup()
        {
            ConfigureStationSettings(200);
        }

        private static void ConfigureStationSettings(int driveCommandIntervalMs)
        {
            StationSettings.Configure(new StationOptions
            {
                StationName = "Test Station",
                TelemetryPort = 0,
                CommandPort = 0,
                CommandHost = "127.0.0.1",
                RosterPath = "Data\\rovers.json",
                SessionCachePath = "Data\\session-cache.bin",
                TrackNorth = 10,
                TrackSouth = 0,
                TrackWest = 0,
                TrackEast = 10,
                DriveCommandIntervalMs = driveCommandIntervalMs,
                LogLevel = RoverRally.Core.Logging.LogLevel.Info,
                LogDirectory = System.IO.Path.GetTempPath()
            });
        }

        [TestMethod]
        public void StartThrowsIfInitializeWasNotCalledFirst()
        {
            StationService service = new StationService();

            Assert.ThrowsExactly<InvalidOperationException>(() => service.Start());
        }

        [TestMethod]
        public void StartIsIdempotentAndReusesTheFirstLinkResources()
        {
            StationService service = new StationService();
            service.Initialize(new GeofenceMonitor(Array.Empty<TrackPoint>()), new LapTimerRegistry(new TrackPoint(0, 0), new TrackPoint(0, 0)));

            try
            {
                service.Start();
                object telemetry = service.TelemetryClient;
                object commands = service.CommandSender;

                service.Start();

                Assert.AreSame(telemetry, service.TelemetryClient);
                Assert.AreSame(commands, service.CommandSender);
            }
            finally
            {
                service.Stop();
            }
        }

        [TestMethod]
        public void StartCanBeRetriedAfterStartupFailure()
        {
            StationService service = new StationService();
            service.Initialize(new GeofenceMonitor(Array.Empty<TrackPoint>()), new LapTimerRegistry(new TrackPoint(0, 0), new TrackPoint(0, 0)));
            try
            {
                ConfigureStationSettings(-1);

                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.Start());
                Assert.ThrowsExactly<InvalidOperationException>(() => _ = service.TelemetryClient);

                ConfigureStationSettings(200);
                service.Start();
                Assert.IsTrue(service.TelemetryClient.IsRunning);
            }
            finally
            {
                service.Stop();
                ConfigureStationSettings(200);
            }
        }

        [TestMethod]
        public void StopIsSafeWhenCalledTwice()
        {
            StationService service = new StationService();
            service.Initialize(new GeofenceMonitor(Array.Empty<TrackPoint>()), new LapTimerRegistry(new TrackPoint(0, 0), new TrackPoint(0, 0)));
            service.Start();

            service.Stop();
            service.Stop();
        }
    }
}
