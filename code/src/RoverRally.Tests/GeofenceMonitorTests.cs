using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    /// <summary>
    /// GeofenceMonitor is the one place a rover's position turns into a
    /// safety alert with a rover's name attached, so every test here asks
    /// the same two questions: is the verdict for this call the verdict for
    /// the position this call was given, and does calling it for one rover
    /// ever change what another rover's call reports? (#4)
    /// </summary>
    [TestClass]
    public class GeofenceMonitorTests
    {
        private const byte SandstormId = 1;
        private const byte MishmishId = 2;

        private static readonly TrackPoint[] Fence = new TrackPoint[]
        {
            new TrackPoint(10, 0),
            new TrackPoint(10, 10),
            new TrackPoint(0, 10),
            new TrackPoint(0, 0)
        };

        private static readonly TrackPoint Inside = new TrackPoint(5, 5);
        private static readonly TrackPoint Outside = new TrackPoint(20, 20);

        [TestMethod]
        public void ReportsInsideWhenThePositionIsWithinTheFence()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            Assert.IsFalse(monitor.IsOutside(SandstormId, Inside));
        }

        [TestMethod]
        public void ReportsOutsideWhenThePositionIsOutsideTheFence()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            Assert.IsTrue(monitor.IsOutside(SandstormId, Outside));
        }

        /// <summary>
        /// A previous version gated every verdict on a "have we seen a
        /// sample yet" flag that started false, so the very first call for
        /// any rover always reported "inside" no matter where it actually
        /// was. A brand new monitor, one call, an outside position - that
        /// must already report outside.
        /// </summary>
        [TestMethod]
        public void TheFirstSampleForARoverIsNotForcedInside()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            Assert.IsTrue(monitor.IsOutside(SandstormId, Outside),
                          "The first sample for a rover was not evaluated on its own merits.");
        }

        /// <summary>
        /// The old implementation reported the *previous* call's position,
        /// so the sample that actually crosses the boundary still came back
        /// "inside" and the alert only fired one sample late. It must fire
        /// on the crossing sample itself.
        /// </summary>
        [TestMethod]
        public void ReportsOutsideAsSoonAsARoverCrossesOut()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            monitor.IsOutside(SandstormId, Inside);

            Assert.IsTrue(monitor.IsOutside(SandstormId, Outside),
                          "The crossing sample itself should already report outside.");
        }

        [TestMethod]
        public void ReportsInsideAsSoonAsARoverCrossesBackIn()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            monitor.IsOutside(SandstormId, Outside);

            Assert.IsFalse(monitor.IsOutside(SandstormId, Inside),
                           "The sample that returns inside should already report inside.");
        }

        /// <summary>
        /// The mis-attribution from #4, reproduced directly: one monitor
        /// serving two rovers whose frames arrive interleaved, the way the
        /// simulator actually sends them. Sandstorm is the vehicle that goes
        /// wide; Mishmish stays on the racing line the whole time. Before the
        /// fix, Sandstorm's excursion showed up as a warning about Mishmish
        /// instead, because "the previous call's position" was whichever
        /// rover's frame had arrived immediately before.
        /// </summary>
        [TestMethod]
        public void InterleavedFramesFromDifferentRoversDoNotContaminateEachOthersVerdict()
        {
            GeofenceMonitor monitor = new GeofenceMonitor(Fence);

            Assert.IsFalse(monitor.IsOutside(MishmishId, Inside), "Mishmish, tick 1: on the racing line.");
            Assert.IsTrue(monitor.IsOutside(SandstormId, Outside), "Sandstorm, tick 1: running wide.");
            Assert.IsFalse(monitor.IsOutside(MishmishId, Inside), "Mishmish, tick 2: still on the racing line.");
            Assert.IsTrue(monitor.IsOutside(SandstormId, Outside), "Sandstorm, tick 2: still running wide.");
            Assert.IsFalse(monitor.IsOutside(MishmishId, Inside), "Mishmish, tick 3: never left, never should warn.");
        }
    }
}
