using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Units;

namespace RoverRally.Tests
{
    [TestClass]
    public class BatteryGaugeTests
    {
        [TestMethod]
        public void ReportsAPercentage()
        {
            int percent = BatteryGauge.ToPercent(11100);

            Assert.IsTrue(percent >= 0);
        }
    }
}
