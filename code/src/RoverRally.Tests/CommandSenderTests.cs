using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    [TestClass]
    public class CommandSenderTests
    {
        /// <summary>
        /// First-ever coverage of CommandSender (#22). Send's own catch block
        /// is otherwise only reachable via a real socket fault, which isn't
        /// deterministic to trigger in a unit test - but Dispose() closing
        /// the underlying UdpClient first makes the next Send() throw
        /// ObjectDisposedException reliably, exercising the same catch path
        /// without any real networking.
        /// </summary>
        [TestMethod]
        public void SendAfterDisposeLogsAnError()
        {
            CapturingLogger logger = new CapturingLogger();
            CommandSender sender = new CommandSender("127.0.0.1", 14551, logger);
            sender.Dispose();

            sender.Send(1, 0, 0, false, false);

            Assert.IsTrue(logger.HasEntry(LogLevel.Error, "Could not send a command to rover 1"));
        }
    }
}
