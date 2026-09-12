using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Models;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    /// <summary>
    /// Exercises the Fleet tab's grid filtering and CSV export commands
    /// directly via constructor injection, without a Window or any real UI
    /// - carried over from #73 to <see cref="FleetViewModel"/> by #88's
    /// split. <see cref="FakeDialogService"/> stands in for the real save
    /// dialog.
    /// </summary>
    [TestClass]
    public class FleetViewModelTests
    {
        private FakeDialogService _dialogs = null!;
        private RoverFleetState _fleetState = null!;
        private FleetViewModel _vm = null!;

        [TestInitialize]
        public void Setup()
        {
            _dialogs = new FakeDialogService();
            _fleetState = new RoverFleetState();
            _vm = new FleetViewModel(_fleetState, _dialogs);
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
