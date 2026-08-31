using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Units;

namespace RoverRally.Tests
{
    [TestClass]
    public class SpeedConverterTests
    {
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

            Assert.AreEqual("0.4 mph", display);
        }
    }
}
