using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    [TestClass]
    public class TrackProjectionTests
    {
        [TestMethod]
        public void PlacesTheStartLine()
        {
            TrackProjection projection = new TrackProjection(32.2830, 32.2770, 34.9160, 34.9250, 720, 480);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(32.2800, 34.9205), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(360, x, 0.5);
            Assert.AreEqual(240, y, 0.5);
        }

        [TestMethod]
        public void ProjectsTheNorthWestCornerToTheOrigin()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(10, 0), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
        }

        [TestMethod]
        public void ProjectsTheNorthEastCornerToTheTopRight()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(10, 10), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(100, x);
            Assert.AreEqual(0, y);
        }

        [TestMethod]
        public void ProjectsTheSouthWestCornerToTheBottomLeft()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(0, 0), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(0, x);
            Assert.AreEqual(100, y);
        }

        [TestMethod]
        public void ProjectsTheSouthEastCornerToTheBottomRight()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(0, 10), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(100, x);
            Assert.AreEqual(100, y);
        }

        [TestMethod]
        public void ProjectsTheCentreToTheMiddleOfTheCanvas()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(5, 5), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(50, x);
            Assert.AreEqual(50, y);
        }

        [TestMethod]
        public void ProjectsAnInteriorPointCorrectlyWithNonSquareBoundsAndARectangularCanvas()
        {
            // Every other case here uses equal lat/lon spans and a square
            // canvas, so a regression that swaps _width/_height or swaps the
            // latitude/longitude spans would still pass them all. Unequal
            // spans (20 x 100) and a rectangular canvas (500 x 200) give x
            // and y different scale factors (x5 vs x10), so a swap changes
            // the projected point.
            TrackProjection projection = new TrackProjection(20, 0, 0, 100, 500, 200);

            double x, y;
            bool result = projection.TryProject(new TrackPoint(15, 40), out x, out y);

            Assert.IsTrue(result);
            Assert.AreEqual(200, x);
            Assert.AreEqual(50, y);
        }

        [TestMethod]
        public void ReturnsFalseForAPointNorthOfTheMappedArea()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            Assert.IsFalse(projection.TryProject(new TrackPoint(11, 5), out x, out y));
        }

        [TestMethod]
        public void ReturnsFalseForAPointSouthOfTheMappedArea()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            Assert.IsFalse(projection.TryProject(new TrackPoint(-1, 5), out x, out y));
        }

        [TestMethod]
        public void ReturnsFalseForAPointWestOfTheMappedArea()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            Assert.IsFalse(projection.TryProject(new TrackPoint(5, -1), out x, out y));
        }

        [TestMethod]
        public void ReturnsFalseForAPointEastOfTheMappedArea()
        {
            TrackProjection projection = new TrackProjection(10, 0, 0, 10, 100, 100);

            double x, y;
            Assert.IsFalse(projection.TryProject(new TrackPoint(5, 11), out x, out y));
        }
    }
}
