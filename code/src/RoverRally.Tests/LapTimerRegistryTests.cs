using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Control;
using RoverRally.Core.Models;

namespace RoverRally.Tests
{
    /// <summary>
    /// The registry's job is get-or-create-and-remember, so these tests are
    /// about the identity of what it hands back and about isolation between
    /// rovers - LapTimerTests already covers crossing/counting behaviour for
    /// a single instance.
    /// </summary>
    [TestClass]
    public class LapTimerRegistryTests
    {
        private const byte FalafelId = 1;
        private const byte SandstormId = 2;

        private static readonly TrackPoint LineStart = new TrackPoint(1, 0);
        private static readonly TrackPoint LineEnd = new TrackPoint(-1, 0);

        private static readonly TrackPoint West = new TrackPoint(0, -5);
        private static readonly TrackPoint East = new TrackPoint(0, 5);

        private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void ReturnsTheSameInstanceForTheSameRoverIdOnRepeatedCalls()
        {
            LapTimerRegistry registry = new LapTimerRegistry(LineStart, LineEnd);

            LapTimer first = registry.For(FalafelId);
            LapTimer second = registry.For(FalafelId);

            Assert.AreSame(first, second, "Two calls for the same rover returned different lap timers.");
        }

        [TestMethod]
        public void ReturnsDifferentInstancesForDifferentRoverIds()
        {
            LapTimerRegistry registry = new LapTimerRegistry(LineStart, LineEnd);

            LapTimer falafel = registry.For(FalafelId);
            LapTimer sandstorm = registry.For(SandstormId);

            Assert.AreNotSame(falafel, sandstorm, "Two different rovers were handed the same lap timer.");
        }

        [TestMethod]
        public void ALapTimerForANeverSeenRoverStartsAtZeroLaps()
        {
            LapTimerRegistry registry = new LapTimerRegistry(LineStart, LineEnd);

            LapTimer timer = registry.For(FalafelId);

            Assert.AreEqual(0, timer.LapCount);
            Assert.IsNull(timer.LastLapTime);
        }

        /// <summary>
        /// Interleaved frames from two rovers, the way the simulator actually
        /// sends them: Falafel completes a lap while Sandstorm is still on
        /// its way to the line for the first time. Neither should see the
        /// other's crossings.
        /// </summary>
        [TestMethod]
        public void InterleavedFramesFromDifferentRoversDoNotContaminateEachOthersLapCount()
        {
            LapTimerRegistry registry = new LapTimerRegistry(LineStart, LineEnd);

            registry.For(FalafelId).Update(West, Start);
            registry.For(SandstormId).Update(West, Start);

            registry.For(FalafelId).Update(East, Start.AddSeconds(1));
            registry.For(SandstormId).Update(West, Start.AddSeconds(1));

            registry.For(FalafelId).Update(West, Start.AddSeconds(2));
            registry.For(SandstormId).Update(West, Start.AddSeconds(2));

            registry.For(FalafelId).Update(East, Start.AddSeconds(3));
            registry.For(SandstormId).Update(West, Start.AddSeconds(3));

            Assert.AreEqual(1, registry.For(FalafelId).LapCount, "Falafel completed one lap.");
            Assert.AreEqual(0, registry.For(SandstormId).LapCount,
                            "Sandstorm never crossed the line and must not have banked Falafel's lap.");
        }
    }
}
