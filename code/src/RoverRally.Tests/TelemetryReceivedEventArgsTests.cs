using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    /// <summary>
    /// Flagged by review on the pull request: a queued dispatcher call can run
    /// long after the frame it carries actually arrived, so re-stamping "now"
    /// when that call finally executes records queueing delay rather than
    /// when the vehicle reported. These tests pin down that the timestamp is
    /// captured at construction - on the listener thread, before any
    /// dispatcher hop - not read later from some other clock.
    /// </summary>
    [TestClass]
    public class TelemetryReceivedEventArgsTests
    {
        [TestMethod]
        public void StampsReceivedUtcAtConstructionTime()
        {
            DateTime before = DateTime.UtcNow;
            TelemetryReceivedEventArgs args = new TelemetryReceivedEventArgs(SampleFrame());
            DateTime after = DateTime.UtcNow;

            Assert.IsTrue(args.ReceivedUtc >= before && args.ReceivedUtc <= after,
                         "ReceivedUtc was not captured at construction time.");
        }

        [TestMethod]
        public void DoesNotMoveWhenReadAgainLater()
        {
            TelemetryReceivedEventArgs args = new TelemetryReceivedEventArgs(SampleFrame());
            DateTime first = args.ReceivedUtc;

            System.Threading.Thread.Sleep(20);

            Assert.AreEqual(first, args.ReceivedUtc,
                           "ReceivedUtc changed on a later read - it must be fixed at construction, " +
                           "not recomputed whenever a queued handler happens to run.");
        }

        private static TelemetryFrame SampleFrame()
        {
            return new TelemetryFrame(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }
}
