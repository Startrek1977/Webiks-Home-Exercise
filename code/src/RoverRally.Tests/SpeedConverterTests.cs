using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Units;

namespace RoverRally.Tests
{
    [TestClass]
    public class SpeedConverterTests
    {
        [TestMethod]
        public void FormatsZeroSpeedInMetric()
        {
            string display = SpeedConverter.Format(0, SpeedUnit.KilometresPerHour);

            Assert.AreEqual("0.0 km/h", display);
        }

        [TestMethod]
        public void FormatsZeroSpeedForTheImperialReadout()
        {
            string display = SpeedConverter.Format(0, SpeedUnit.MilesPerHour);

            Assert.AreEqual("0.0 mph", display);
        }

        [TestMethod]
        public void FormatsCruiseSpeedInMetric()
        {
            string display = SpeedConverter.Format(500, SpeedUnit.KilometresPerHour);

            Assert.AreEqual("18.0 km/h", display);
        }

        [TestMethod]
        public void FormatsCruiseSpeedForTheImperialReadout()
        {
            string display = SpeedConverter.Format(500, SpeedUnit.MilesPerHour);

            Assert.AreEqual("11.2 mph", display);
        }

        [TestMethod]
        public void FormatsMaximumControllerSpeedInMetric()
        {
            string display = SpeedConverter.Format(ushort.MaxValue, SpeedUnit.KilometresPerHour);

            Assert.AreEqual("2359.3 km/h", display);
        }

        [TestMethod]
        public void FormatsMaximumControllerSpeedForTheImperialReadout()
        {
            string display = SpeedConverter.Format(ushort.MaxValue, SpeedUnit.MilesPerHour);

            Assert.AreEqual("1466.0 mph", display);
        }
    }
}
