using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RoverRally.Tests
{
    [TestClass]
    public class PlatformTests
    {
        /// <summary>
        /// Regression guard for #16: the solution builds x64-only now (no
        /// AnyCPU configuration exists to fall back to), but a build system
        /// misconfiguration could still launch a 32-bit test host under
        /// WOW64. This fails loudly if that ever happens again, rather than
        /// relying on a one-time manual Task Manager check.
        /// </summary>
        [TestMethod]
        public void RunsAsA64BitProcess()
        {
            Assert.IsTrue(Environment.Is64BitProcess);
        }
    }
}
