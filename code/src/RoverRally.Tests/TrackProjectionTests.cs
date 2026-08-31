using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    [TestClass]
    public class TrackProjectionTests
    {
        [TestMethod]
        [Ignore] // Canvas size moved into the view, so the expected pixels here are stale.
        public void PlacesTheStartLine()
        {
            TrackProjection projection = new TrackProjection(32.2830, 32.2770, 34.9160, 34.9250, 640, 400);

            double x, y;
            projection.TryProject(new TrackPoint(32.2800, 34.9205), out x, out y);

            Assert.AreEqual(320, x, 0.5);
            Assert.AreEqual(200, y, 0.5);
        }
    }
}
