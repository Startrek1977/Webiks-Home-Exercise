using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Control;
using RoverRally.Core.Models;
using RoverRally.Core.Units;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    /// <summary>
    /// Covers the one piece of #88 with no precedent elsewhere in the
    /// codebase: <see cref="RoverFleetState"/>/<see cref="SpeedUnitState"/>
    /// are shared, standalone objects injected independently into more than
    /// one view model, precisely so none of them has to reference another
    /// directly. That only works if each dependent view model actually
    /// re-raises its own <c>PropertyChanged</c> when the shared object's own
    /// value changes - these tests exercise that synchronization across the
    /// tab boundary it crosses, which <see cref="TrackViewModelTests"/>,
    /// <see cref="FleetViewModelTests"/>, and <see cref="SettingsViewModelTests"/>
    /// (each wired to its own private state instance) cannot see.
    /// </summary>
    [TestClass]
    public class SharedViewModelStateTests
    {
        [TestMethod]
        public void ChangingSpeedUnitThroughSettingsViewModelUpdatesTracksSelectedSpeedDisplay()
        {
            SpeedUnitState speedUnitState = new SpeedUnitState();
            RoverFleetState fleetState = new RoverFleetState();
            TrackViewModel track = new TrackViewModel(new FakeStationService(), new DriveControllerRegistry(),
                new FakeDialogService(), fleetState, speedUnitState);
            SettingsViewModel settings = new SettingsViewModel(speedUnitState);

            Rover rover = new Rover { Id = 1, Name = "Falafel", SpeedCmS = 500 };
            fleetState.Rovers.Add(rover);
            fleetState.SelectedRover = rover;

            bool raised = false;
            track.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TrackViewModel.SelectedSpeedDisplay)) raised = true;
            };

            settings.SpeedUnit = SpeedUnit.MilesPerHour;

            Assert.IsTrue(raised,
                "TrackViewModel.SelectedSpeedDisplay should re-raise PropertyChanged when the shared SpeedUnitState changes.");
            Assert.AreEqual(SpeedConverter.Format(rover.SpeedCmS, SpeedUnit.MilesPerHour), track.SelectedSpeedDisplay);
        }

        [TestMethod]
        public void SelectingARoverThroughFleetViewModelUpdatesTracksSelectedRover()
        {
            RoverFleetState fleetState = new RoverFleetState();
            TrackViewModel track = new TrackViewModel(new FakeStationService(), new DriveControllerRegistry(),
                new FakeDialogService(), fleetState, new SpeedUnitState());
            FleetViewModel fleet = new FleetViewModel(fleetState, new FakeDialogService());

            Rover rover = new Rover { Id = 9, Name = "Comet" };
            fleetState.Rovers.Add(rover);

            bool selectedRoverRaised = false;
            bool hasSelectionRaised = false;
            track.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TrackViewModel.SelectedRover)) selectedRoverRaised = true;
                if (e.PropertyName == nameof(TrackViewModel.HasSelection)) hasSelectionRaised = true;
            };

            fleet.SelectedRover = rover;

            Assert.IsTrue(selectedRoverRaised,
                "TrackViewModel.SelectedRover should re-raise PropertyChanged when the shared RoverFleetState's selection changes.");
            Assert.IsTrue(hasSelectionRaised,
                "TrackViewModel.HasSelection should also refresh, the same cascade OnSelectedRoverChanged gave it before the split.");
            Assert.AreSame(rover, track.SelectedRover);
        }
    }
}
