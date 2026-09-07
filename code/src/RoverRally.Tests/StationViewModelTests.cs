using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Control;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;
using RoverRally.Core.Units;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    /// <summary>
    /// Exercises the command layer directly via constructor injection,
    /// without a Window, a simulator, or any real UI - the point of #73.
    /// <see cref="FakeStationService"/> and <see cref="FakeDialogService"/>
    /// stand in for the real link and real Windows dialogs; the real
    /// <see cref="DriveControllerRegistry"/> is used as-is since it has no
    /// side effects of its own and is already covered directly by
    /// <see cref="DriveControllerRegistryTests"/>.
    /// </summary>
    [TestClass]
    public class StationViewModelTests
    {
        private FakeStationService _service = null!;
        private FakeDialogService _dialogs = null!;
        private FakeDispatcherService _dispatcher = null!;
        private DriveControllerRegistry _driveControllers = null!;
        private StationViewModel _vm = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new FakeStationService();
            _dialogs = new FakeDialogService();
            _dispatcher = new FakeDispatcherService();
            _driveControllers = new DriveControllerRegistry();
            _vm = new StationViewModel(_service, _driveControllers, _dialogs, _dispatcher);
        }

        [TestMethod]
        public void ArmCommandWithNoSelectedRoverShowsAMessageInsteadOfThrowing()
        {
            _vm.ArmCommand.Execute(null);

            Assert.AreEqual(1, _dialogs.Messages.Count);
            StringAssert.Contains(_dialogs.Messages[0], "Select a rover first");
        }

        [TestMethod]
        public void ArmCommandWithACentredThrottleArmsTheSelectedRoversController()
        {
            Rover rover = new Rover { Id = 3, Name = "Falafel" };
            _vm.Rovers.Add(rover);
            _vm.SelectedRover = rover;
            _vm.ThrottleValue = 0;

            _vm.ArmCommand.Execute(null);

            Assert.IsTrue(_driveControllers.For(rover.Id).IsArmed);
            Assert.AreEqual("DISARM", _vm.ArmButtonText);
        }

        [TestMethod]
        public void ArmCommandRefusesToClearALatchedStopWhileTheThrottleIsOffCentre()
        {
            Rover rover = new Rover { Id = 4, Name = "Sandstorm" };
            _vm.Rovers.Add(rover);
            _vm.SelectedRover = rover;

            // Latch a stop first, the same way the EMERGENCY STOP button does.
            _vm.EmergencyStopCommand.Execute(null);
            Assert.IsTrue(_driveControllers.For(rover.Id).IsEmergencyStopLatched);

            _vm.ThrottleValue = 500;
            _vm.ArmCommand.Execute(null);

            Assert.IsFalse(_driveControllers.For(rover.Id).IsArmed,
                           "Re-arming with an off-centre throttle should have been refused.");
            Assert.IsTrue(_driveControllers.For(rover.Id).IsEmergencyStopLatched,
                          "The latch should still be held after a refused re-arm.");
        }

        [TestMethod]
        public void EmergencyStopCommandWithNoSelectedRoverDoesNothing()
        {
            _vm.EmergencyStopCommand.Execute(null);

            Assert.AreEqual(0, _dialogs.Messages.Count);
        }

        [TestMethod]
        public void EmergencyStopCommandLatchesTheSelectedRoversControllerAndNotifiesTheOperator()
        {
            Rover rover = new Rover { Id = 5, Name = "Comet" };
            _vm.Rovers.Add(rover);
            _vm.SelectedRover = rover;

            _vm.EmergencyStopCommand.Execute(null);

            Assert.IsTrue(_driveControllers.For(rover.Id).IsEmergencyStopLatched);
            Assert.IsTrue(_vm.DriveStateIsLatched);
            Assert.AreEqual(1, _dialogs.Messages.Count);
            StringAssert.Contains(_dialogs.Messages[0], rover.Name);
        }

        [TestMethod]
        public void SetSpeedUnitCommandUpdatesTheSpeedUnitProperty()
        {
            _vm.SetSpeedUnitCommand.Execute(SpeedUnit.MilesPerHour);

            Assert.AreEqual(SpeedUnit.MilesPerHour, _vm.SpeedUnit);
        }

        [TestMethod]
        public void FilterTextNarrowsTheFleetViewToMatchingRoverNames()
        {
            _vm.Rovers.Add(new Rover { Id = 1, Name = "Falafel" });
            _vm.Rovers.Add(new Rover { Id = 2, Name = "Sandstorm" });

            _vm.FilterText = "sand";

            Assert.IsTrue(Contains(_vm.RoversView, "Sandstorm"));
            Assert.IsFalse(Contains(_vm.RoversView, "Falafel"));
        }

        /// <summary>
        /// The code-behind this replaced trimmed SearchBox.Text before
        /// matching (Copilot review on #78) - FilterText itself keeps
        /// whatever the operator typed, but matching must still ignore
        /// surrounding whitespace the same way.
        /// </summary>
        [TestMethod]
        public void FilterTextWithSurroundingWhitespaceStillMatches()
        {
            _vm.Rovers.Add(new Rover { Id = 1, Name = "Sandstorm" });

            _vm.FilterText = "  sand  ";

            Assert.IsTrue(Contains(_vm.RoversView, "Sandstorm"));
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
            _vm.Rovers.Add(rover);

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
        public void SelectedStatusFilterNarrowsTheFleetViewToMatchingStatus()
        {
            _vm.Rovers.Add(new Rover { Id = 1, Name = "Falafel", Status = RoverStatus.Idle });
            _vm.Rovers.Add(new Rover { Id = 2, Name = "Sandstorm", Status = RoverStatus.Driving });

            _vm.SelectedStatusFilter = "Driving";

            Assert.IsTrue(Contains(_vm.RoversView, "Sandstorm"));
            Assert.IsFalse(Contains(_vm.RoversView, "Falafel"));
        }

        [TestMethod]
        public void ExportHistoryCommandWithNoCompletedRunsShowsAMessageInsteadOfOpeningASaveDialog()
        {
            _dialogs.SaveFileDialogResult = "should-not-be-used.csv";

            _vm.ExportHistoryCommand.Execute(null);

            Assert.AreEqual(1, _dialogs.Messages.Count);
            StringAssert.Contains(_dialogs.Messages[0], "no completed runs");
        }

        private static bool Contains(ICollectionView view, string roverName)
        {
            foreach (object item in view)
            {
                if (item is Rover rover && rover.Name == roverName) return true;
            }

            return false;
        }
    }
}
