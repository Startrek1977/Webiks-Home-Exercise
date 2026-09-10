using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.App.Views;

namespace RoverRally.Tests
{
    [TestClass]
    public class RoverTrailTests
    {
        [TestMethod]
        public void TheFirstPointEverAddedStartsASegment()
        {
            RoverTrail trail = new RoverTrail(300);

            trail.AddPoint(1, 2);

            Assert.AreEqual(1, trail.Segments.Count);
            Assert.AreEqual(1, trail.Segments[0].Count);
            Assert.AreEqual(new Point(1, 2), trail.Segments[0][0]);
        }

        [TestMethod]
        public void ConsecutivePointsWithNoGapStayInOneSegment()
        {
            RoverTrail trail = new RoverTrail(300);

            trail.AddPoint(1, 1);
            trail.AddPoint(2, 2);
            trail.AddPoint(3, 3);

            Assert.AreEqual(1, trail.Segments.Count);
            Assert.AreEqual(3, trail.Segments[0].Count);
        }

        [TestMethod]
        public void ARecordedGapStartsANewSegmentOnTheNextPointWithoutTouchingTheFirst()
        {
            RoverTrail trail = new RoverTrail(300);
            trail.AddPoint(1, 1);
            trail.AddPoint(2, 2);

            trail.RecordGap();
            trail.AddPoint(9, 9);

            Assert.AreEqual(2, trail.Segments.Count);
            Assert.AreEqual(2, trail.Segments[0].Count);
            Assert.AreEqual(new Point(1, 1), trail.Segments[0][0]);
            Assert.AreEqual(new Point(2, 2), trail.Segments[0][1]);
            Assert.AreEqual(1, trail.Segments[1].Count);
            Assert.AreEqual(new Point(9, 9), trail.Segments[1][0]);
        }

        /// <summary>
        /// A rover that stays off-track for several ticks calls RecordGap
        /// once per skipped tick - the trail must not fragment into one new
        /// segment per skipped tick once it comes back.
        /// </summary>
        [TestMethod]
        public void SeveralConsecutiveRecordedGapsStillStartOnlyOneNewSegment()
        {
            RoverTrail trail = new RoverTrail(300);
            trail.AddPoint(1, 1);

            trail.RecordGap();
            trail.RecordGap();
            trail.RecordGap();
            trail.AddPoint(9, 9);

            Assert.AreEqual(2, trail.Segments.Count);
            Assert.AreEqual(1, trail.Segments[1].Count);
        }

        [TestMethod]
        public void AddingMorePointsThanTheCapKeepsOnlyTheNewestOnes()
        {
            RoverTrail trail = new RoverTrail(3);

            trail.AddPoint(1, 1);
            trail.AddPoint(2, 2);
            trail.AddPoint(3, 3);
            trail.AddPoint(4, 4);

            Assert.AreEqual(1, trail.Segments.Count);
            Assert.AreEqual(3, trail.Segments[0].Count);
            Assert.AreEqual(new Point(2, 2), trail.Segments[0][0]);
            Assert.AreEqual(new Point(3, 3), trail.Segments[0][1]);
            Assert.AreEqual(new Point(4, 4), trail.Segments[0][2]);
        }

        /// <summary>
        /// With two segments whose combined count exceeds the cap, trimming
        /// removes from the oldest segment first and drops it entirely once
        /// it's empty - the same "last N points total, oldest expire first"
        /// invariant the single-Polyline trail had before #80, just spread
        /// across however many segments a gap has produced.
        /// </summary>
        [TestMethod]
        public void TrimmingAcrossSegmentsRemovesFromTheOldestFirstAndDropsItOnceEmpty()
        {
            RoverTrail trail = new RoverTrail(3);
            trail.AddPoint(1, 1);
            trail.AddPoint(2, 2);
            trail.RecordGap();
            trail.AddPoint(3, 3);

            trail.AddPoint(4, 4);

            Assert.AreEqual(2, trail.Segments.Count);
            Assert.AreEqual(1, trail.Segments[0].Count);
            Assert.AreEqual(new Point(2, 2), trail.Segments[0][0]);
            Assert.AreEqual(2, trail.Segments[1].Count);

            trail.AddPoint(5, 5);

            Assert.AreEqual(1, trail.Segments.Count, "The first segment must be dropped entirely once emptied.");
            Assert.AreEqual(3, trail.Segments[0].Count);
            Assert.AreEqual(new Point(3, 3), trail.Segments[0][0]);
            Assert.AreEqual(new Point(4, 4), trail.Segments[0][1]);
            Assert.AreEqual(new Point(5, 5), trail.Segments[0][2]);
        }

        [TestMethod]
        public void AddingExactlyTheCapDoesNotTrimAnything()
        {
            RoverTrail trail = new RoverTrail(3);

            trail.AddPoint(1, 1);
            trail.AddPoint(2, 2);
            trail.AddPoint(3, 3);

            Assert.AreEqual(1, trail.Segments.Count);
            Assert.AreEqual(3, trail.Segments[0].Count);
        }
    }
}
