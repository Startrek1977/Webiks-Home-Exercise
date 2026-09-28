using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Configuration;

namespace RoverRally.Tests
{
    [TestClass]
    public class StationSettingsTests
    {
        /// <summary>
        /// Other test classes call <see cref="StationSettings.Configure"/>, so the
        /// static options are cleared first to make this independent of test
        /// execution order.
        /// </summary>
        [TestMethod]
        public void ReadingAConfigBackedPropertyBeforeConfigureThrows()
        {
            typeof(StationSettings)
                .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .SetValue(null, null);

            Assert.ThrowsExactly<InvalidOperationException>(() => _ = StationSettings.TelemetryPort);
        }

        [TestMethod]
        public void BuildProfileKeyNameIsDeterministicForTheSameUsername()
        {
            string first = StationSettings.BuildProfileKeyName("jdoe");
            string second = StationSettings.BuildProfileKeyName("jdoe");

            Assert.AreEqual(first, second);
            Assert.AreEqual("jdoe", first);
        }

        [TestMethod]
        public void BuildProfileKeyNameGivesDifferentUsernamesDifferentSlots()
        {
            string first = StationSettings.BuildProfileKeyName("jdoe");
            string second = StationSettings.BuildProfileKeyName("asmith");

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void BuildProfileKeyNameSanitizesRegistryUnsafeCharacters()
        {
            string sanitized = StationSettings.BuildProfileKeyName(@"DOMAIN\j.doe smith!");

            Assert.AreEqual("DOMAIN_j.doe_smith_", sanitized);
        }

        [TestMethod]
        public void BuildProfileKeyNameFallsBackForANullUsername()
        {
            Assert.AreEqual("unknown", StationSettings.BuildProfileKeyName(null));
        }

        [TestMethod]
        public void BuildProfileKeyNameFallsBackForAnEmptyUsername()
        {
            Assert.AreEqual("unknown", StationSettings.BuildProfileKeyName(string.Empty));
        }

        [TestMethod]
        public void BuildProfileKeyNameReplacesEveryUnsafeCharacterOneForOneRatherThanCollapsingThem()
        {
            Assert.AreEqual("___", StationSettings.BuildProfileKeyName(@"\\\"));
        }
    }
}
