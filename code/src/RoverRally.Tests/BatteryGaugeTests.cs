using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Units;
using RoverRally.Tests.TestSupport;

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
            Assert.AreEqual(100, BatteryGauge.ToPercent(BatteryGauge.FullMilliVolts));
        }

        [TestMethod]
        public void ClampsToZeroBelowEmpty()
        {
            Assert.AreEqual(0, BatteryGauge.ToPercent(BatteryGauge.EmptyMilliVolts - 100));
        }

        [TestMethod]
        public void ClampsToOneHundredAboveFull()
        {
            Assert.AreEqual(100, BatteryGauge.ToPercent(BatteryGauge.FullMilliVolts + 400));
        }

        [TestMethod]
        public void TreatsAFailedSensorReadingAsEmptyNotFull()
        {
            Assert.AreEqual(0, BatteryGauge.ToPercent(0));
        }

        [TestMethod]
        public void IsCriticalFiresForANearlyFlatPack()
        {
            Assert.IsTrue(BatteryGauge.IsCritical(BatteryGauge.EmptyMilliVolts + 200));
        }

        [TestMethod]
        public void IsCriticalFiresForAFailedSensorReading()
        {
            Assert.IsTrue(BatteryGauge.IsCritical(0));
        }

        [TestMethod]
        public void LogsAWarningWhenAReadingClampsLow()
        {
            CapturingLogger logger = new CapturingLogger();

            BatteryGauge.ToPercent(BatteryGauge.EmptyMilliVolts - 100, logger);

            Assert.IsTrue(logger.HasEntry(LogLevel.Warning, "before clamping"));
        }

        [TestMethod]
        public void LogsAWarningWhenAReadingClampsHigh()
        {
            CapturingLogger logger = new CapturingLogger();

            BatteryGauge.ToPercent(BatteryGauge.FullMilliVolts + 400, logger);

            Assert.IsTrue(logger.HasEntry(LogLevel.Warning, "before clamping"));
        }

        [TestMethod]
        public void LogsNothingForAnInRangeReading()
        {
            CapturingLogger logger = new CapturingLogger();

            BatteryGauge.ToPercent(11100, logger);

            Assert.AreEqual(0, logger.Entries.Count);
        }
    }
}
