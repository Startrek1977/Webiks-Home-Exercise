using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    [TestClass]
    public class TelemetryClientTests
    {
        /// <summary>
        /// First-ever coverage of TelemetryClient (#22). Start(0) asks the OS
        /// for an ephemeral port, so this can't collide with anything else
        /// running on the machine. The "listener started" log happens
        /// synchronously in Start() itself, before the background listener
        /// thread's socket is even opened, so this is deterministic without
        /// waiting on that thread.
        /// </summary>
        [TestMethod]
        public void StartLogsThatTheListenerStarted()
        {
            CapturingLogger logger = new CapturingLogger();
            TelemetryClient client = new TelemetryClient(logger);

            try
            {
                client.Start(0);

                Assert.IsTrue(logger.HasEntry(LogLevel.Information, "Telemetry listener started"));
            }
            finally
            {
                client.Stop();
            }
        }
    }
}
