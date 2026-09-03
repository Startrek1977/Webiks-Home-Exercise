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

        [TestMethod]
        public void ReportsZeroPercentForAFlatPack()
        {
            Assert.AreEqual(0, BatteryGauge.ToPercent(BatteryGauge.EmptyMilliVolts));
        }

        [TestMethod]
        public void ReportsOneHundredPercentForAFullPack()
        {
            Assert.AreEqual(100, BatteryGauge.ToPercent(12600));
        }

        [TestMethod]
        public void ClampsToZeroBelowEmpty()
        {
            Assert.AreEqual(0, BatteryGauge.ToPercent(8900));
        }

        [TestMethod]
        public void ClampsToOneHundredAboveFull()
        {
            Assert.AreEqual(100, BatteryGauge.ToPercent(13000));
        }

        [TestMethod]
        public void TreatsAFailedSensorReadingAsEmptyNotFull()
        {
            Assert.AreEqual(0, BatteryGauge.ToPercent(0));
        }

        [TestMethod]
        public void IsCriticalFiresForANearlyFlatPack()
        {
            Assert.IsTrue(BatteryGauge.IsCritical(9200));
        }

        [TestMethod]
        public void IsCriticalFiresForAFailedSensorReading()
        {
            Assert.IsTrue(BatteryGauge.IsCritical(0));
        }
    }
}
