using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Control;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    /// <summary>
    /// The line here is a vertical line at longitude 0, running from
    /// <see cref="LineStart"/> (north) to <see cref="LineEnd"/> (south) -
    /// the same shape as the station's real start line, just with round
    /// numbers. Crossing from <see cref="West"/> to <see cref="East"/> is
    /// the racing direction; the reverse is not.
    /// </summary>
    [TestClass]
    public class LapTimerTests
    {
        private static readonly TrackPoint LineStart = new TrackPoint(1, 0);
        private static readonly TrackPoint LineEnd = new TrackPoint(-1, 0);

        private static readonly TrackPoint West = new TrackPoint(0, -5);
        private static readonly TrackPoint East = new TrackPoint(0, 5);
        private static readonly TrackPoint OnLine = new TrackPoint(0, 0);

        private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void TheFirstSampleForARoverIsNeverACrossing()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);

            bool completedLap = timer.Update(East, Start);

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }

        [TestMethod]
        public void AStationarySegmentIsNotACrossing()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);

            bool completedLap = timer.Update(West, Start.AddSeconds(1));

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }

        [TestMethod]
        public void ASegmentThatStaysOnOneSideOfTheLineIsNotACrossing()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(new TrackPoint(0, -8), Start);

            bool completedLap = timer.Update(West, Start.AddSeconds(1));

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }

        [TestMethod]
        public void ReachingTheLineWithoutPassingItIsNotACrossing()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);

            bool completedLap = timer.Update(OnLine, Start.AddSeconds(1));

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }

        /// <summary>
        /// A fix landing exactly on the line is not a fluke to guard against
        /// "just in case" - the line is derived from the same linear
        /// projection as the rover's own quantized lat/lon, so an exact
        /// match is a real, reachable case, not a contrived one. A fix here
        /// must not be lost: the crossing has to be picked up on the next
        /// fix that clears to the far side, exactly as if the middle fix had
        /// never been sampled.
        /// </summary>
        [TestMethod]
        public void AFixExactlyOnTheLineDoesNotLoseTheCrossingOnceTheNextFixClearsIt()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);
            timer.Update(OnLine, Start.AddSeconds(1));

            bool completedLap = timer.Update(East, Start.AddSeconds(2));

            Assert.IsFalse(completedLap, "The first forward crossing only arms the timer.");
            Assert.AreEqual(0, timer.LapCount);

            // A second full pass - still stepping through the same on-line
            // point in between - must complete a lap, proving the pending
            // on-line fix isn't left permanently "used up" by the first pass.
            timer.Update(West, Start.AddSeconds(3));
            timer.Update(OnLine, Start.AddSeconds(4));
            bool secondCompletedLap = timer.Update(East, Start.AddSeconds(5));

            Assert.IsTrue(secondCompletedLap);
            Assert.AreEqual(1, timer.LapCount);
        }

        /// <summary>
        /// Sitting exactly on the line does not, by itself, register as
        /// ever having "arrived" on either side - so drifting back the way
        /// it came must not be counted as a crossing either.
        /// </summary>
        [TestMethod]
        public void RetreatingFromTheLineBackToTheSameSideIsNotACrossing()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);
            timer.Update(OnLine, Start.AddSeconds(1));

            bool completedLap = timer.Update(West, Start.AddSeconds(2));

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }

        [TestMethod]
        public void TheFirstForwardCrossingArmsTheTimerWithoutCompletingALap()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);

            bool completedLap = timer.Update(East, Start.AddSeconds(5));

            Assert.IsFalse(completedLap, "The first crossing has no prior lap to complete.");
            Assert.AreEqual(0, timer.LapCount);
            Assert.IsNull(timer.LastLapTime);
        }

        [TestMethod]
        public void ASecondForwardCrossingCompletesALapAndRecordsItsTime()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);
            timer.Update(East, Start.AddSeconds(5));
            timer.Update(West, Start.AddSeconds(10));

            bool completedLap = timer.Update(East, Start.AddSeconds(22));

            Assert.IsTrue(completedLap);
            Assert.AreEqual(1, timer.LapCount);
            Assert.AreEqual(TimeSpan.FromSeconds(17), timer.LastLapTime);
        }

        [TestMethod]
        public void RepeatedForwardCrossingsKeepIncrementingTheCount()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            DateTime t = Start;
            timer.Update(West, t);
            t = t.AddSeconds(5);
            timer.Update(East, t); // Arms the timer; not a completed lap yet.

            for (int lap = 1; lap <= 3; lap++)
            {
                t = t.AddSeconds(5);
                timer.Update(West, t);
                t = t.AddSeconds(5);
                timer.Update(East, t);

                Assert.AreEqual(lap, timer.LapCount);
            }
        }

        [TestMethod]
        public void AReverseCrossingBetweenTwoForwardOnesDoesNotChangeTheCount()
        {
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(West, Start);
            timer.Update(East, Start.AddSeconds(5));

            bool reverseCompleted = timer.Update(West, Start.AddSeconds(6));

            Assert.IsFalse(reverseCompleted, "Driving back across the line must not itself complete a lap.");
            Assert.AreEqual(0, timer.LapCount);

            bool forwardCompleted = timer.Update(East, Start.AddSeconds(11));

            Assert.IsTrue(forwardCompleted);
            Assert.AreEqual(1, timer.LapCount);
        }

        [TestMethod]
        public void ACrossingOutsideTheLinesSpanIsNotCounted()
        {
            // Same longitude crossing (0) as the real line, but far north of
            // where the line actually runs (latitude 5, versus the line's
            // -1..1 span) - this must not register, or a rover cutting the
            // corner well off the track surface would still bank laps.
            LapTimer timer = new LapTimer(LineStart, LineEnd);
            timer.Update(new TrackPoint(5, -5), Start);

            bool completedLap = timer.Update(new TrackPoint(5, 5), Start.AddSeconds(1));

            Assert.IsFalse(completedLap);
            Assert.AreEqual(0, timer.LapCount);
        }
    }
}
