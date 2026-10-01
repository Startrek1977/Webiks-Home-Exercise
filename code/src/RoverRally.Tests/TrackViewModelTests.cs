using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Control;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    /// <summary>
    /// Exercises the Track tab's drive-safety commands directly via
    /// constructor injection, without a Window, a simulator, or any real UI
    /// - the point of #73, carried over to <see cref="TrackViewModel"/> by
    /// #88's split. <see cref="FakeStationService"/> and
    /// <see cref="FakeDialogService"/> stand in for the real link and real
    /// Windows dialogs; the real <see cref="DriveControllerRegistry"/> is
    /// used as-is since it has no side effects of its own and is already
    /// covered directly by <see cref="DriveControllerRegistryTests"/>.
    /// </summary>
    [TestClass]
    public class TrackViewModelTests
    {
        private FakeStationService _service = null!;
        private FakeDialogService _dialogs = null!;
        private DriveControllerRegistry _driveControllers = null!;
        private RoverFleetState _fleetState = null!;
        private TrackViewModel _vm = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new FakeStationService();
            _dialogs = new FakeDialogService();
            _driveControllers = new DriveControllerRegistry();
            _fleetState = new RoverFleetState();
            _vm = new TrackViewModel(_service, _driveControllers, _dialogs, _fleetState, new SpeedUnitState());
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
            _fleetState.Rovers.Add(rover);
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
            _fleetState.Rovers.Add(rover);
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
        public void EmergencyStopCommandWithNoSelectedRoverWarnsTheOperator()
        {
            _vm.EmergencyStopCommand.Execute(null);

            Assert.AreEqual(1, _dialogs.Messages.Count);
            StringAssert.Contains(_dialogs.Messages[0], "Select a rover first");
        }

        [TestMethod]
        public void EmergencyStopCommandLatchesTheSelectedRoversControllerAndNotifiesTheOperator()
        {
            Rover rover = new Rover { Id = 5, Name = "Comet" };
            _fleetState.Rovers.Add(rover);
            _vm.SelectedRover = rover;

            _vm.EmergencyStopCommand.Execute(null);

            Assert.IsTrue(_driveControllers.For(rover.Id).IsEmergencyStopLatched);
            Assert.IsTrue(_vm.DriveStateIsLatched);
            Assert.AreEqual(1, _dialogs.Messages.Count);
            StringAssert.Contains(_dialogs.Messages[0], rover.Name);
        }

        [TestMethod]
        public void OnFrameAppliedIgnoresARoverThatIsNotInTheRoster()
        {
            Rover outsider = new Rover { Id = 9, Name = "Outsider" };
            TelemetryFrame frame = new TelemetryFrame(outsider.Id, 1, 0, 0, 0, 0, 0, 12000, 80, 200, 0, statusFlags: 0x08);

            _vm.OnFrameApplied(outsider, frame, DateTime.UtcNow);

            Assert.AreEqual(0, GetLapTimerCount(_service.LapTimers));
        }

        private static int GetLapTimerCount(LapTimerRegistry registry)
        {
            System.Reflection.FieldInfo field = typeof(LapTimerRegistry)
                .GetField("_timers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            System.Collections.IDictionary timers = (System.Collections.IDictionary)field.GetValue(registry)!;
            return timers.Count;
        }
    }
}
