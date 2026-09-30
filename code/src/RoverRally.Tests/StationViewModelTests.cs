using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Control;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    /// <summary>
    /// Exercises what's left of the composition root after #88's split:
    /// telemetry-frame ingestion into the shared roster and link-state
    /// tracking. Everything tab-specific moved to
    /// <see cref="TrackViewModelTests"/>/<see cref="FleetViewModelTests"/>/
    /// <see cref="SettingsViewModelTests"/>. <see cref="FakeStationService"/>
    /// and <see cref="FakeDispatcherService"/> stand in for the real link and
    /// the real WPF dispatcher - the point of #73.
    /// </summary>
    [TestClass]
    public class StationViewModelTests
    {
        private FakeStationService _service = null!;
        private FakeDispatcherService _dispatcher = null!;
        private RoverFleetState _fleetState = null!;
        private StationViewModel _vm = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new FakeStationService();
            _dispatcher = new FakeDispatcherService();
            _fleetState = new RoverFleetState();
            SpeedUnitState speedUnitState = new SpeedUnitState();
            FakeDialogService dialogs = new FakeDialogService();

            TrackViewModel track = new TrackViewModel(_service, new DriveControllerRegistry(), dialogs, _fleetState, speedUnitState);
            FleetViewModel fleet = new FleetViewModel(_fleetState, dialogs);
            SettingsViewModel settings = new SettingsViewModel(speedUnitState);

            _vm = new StationViewModel(_service, _dispatcher, _fleetState, track, fleet, settings);
        }

        /// <summary>
        /// Copilot review on #78: Application.Current.Dispatcher is null
        /// outside a running WPF application (including under a test
        /// runner), which would throw the moment this handler ran in a
        /// test - exactly what #73 exists to make reachable. FakeDispatcherService
        /// runs the action inline, and the handlers are wired in the
        /// constructor (not StartLink, which needs a configured
        /// StationSettings this test never sets up), so raising the fake's
        /// event exercises the real handler with no window and no StartLink call.
        /// </summary>
        [TestMethod]
        public void ConnectionStateChangedUpdatesLinkStateWithoutThrowing()
        {
            _service.RaiseConnectionStateChanged();

            Assert.AreEqual("Listening", _vm.LinkState);
        }

        [TestMethod]
        public void FrameReceivedAppliesTheFrameToTheMatchingRoverWithoutThrowing()
        {
            Rover rover = new Rover { Id = 7, Name = "Mishmish" };
            _fleetState.Rovers.Add(rover);

            TelemetryFrame frame = new TelemetryFrame(
                roverId: 7, sequence: 1, timestampMs: 0,
                latitudeE7: 0, longitudeE7: 0,
                headingDeci: 900, speedCmS: 250, batteryMilliVolts: 12000,
                signalPercent: 80, motorTempDeciC: 200, tiltDeciDeg: 0,
                statusFlags: 0);

            _service.RaiseFrameReceived(frame);

            Assert.AreEqual(250, rover.SpeedCmS);
        }

        [TestMethod]
        public void FrameReceivedCountsFramesEvenWhenTheRoverIsUnknown()
        {
            TelemetryFrame frame = new TelemetryFrame(
                roverId: 99, sequence: 1, timestampMs: 0,
                latitudeE7: 0, longitudeE7: 0,
                headingDeci: 900, speedCmS: 250, batteryMilliVolts: 12000,
                signalPercent: 80, motorTempDeciC: 200, tiltDeciDeg: 0,
                statusFlags: 0);

            _service.RaiseFrameReceived(frame);

            Assert.AreEqual("1 frames", _vm.FrameCounterDisplay);
        }
    }
}
