using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.ViewModels;
using RoverRally.Core.Units;

namespace RoverRally.Tests
{
    /// <summary>
    /// Exercises the Settings tab's speed-unit toggle directly via
    /// constructor injection - carried over from #73 to
    /// <see cref="SettingsViewModel"/> by #88's split.
    /// </summary>
    [TestClass]
    public class SettingsViewModelTests
    {
        private SettingsViewModel _vm = null!;

        [TestInitialize]
        public void Setup()
        {
            _vm = new SettingsViewModel(new SpeedUnitState());
        }

        [TestMethod]
        public void SetSpeedUnitCommandUpdatesTheSpeedUnitProperty()
        {
            _vm.SetSpeedUnitCommand.Execute(SpeedUnit.MilesPerHour);

            Assert.AreEqual(SpeedUnit.MilesPerHour, _vm.SpeedUnit);
        }
    }
}
