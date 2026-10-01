using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Configuration;

namespace RoverRally.Tests
{
    [TestClass]
    public class StationSettingsTests
    {
        [TestMethod]
        [DoNotParallelize]
        public void ReadingAConfigBackedPropertyBeforeConfigureThrows()
        {
            FieldInfo optionsField = typeof(StationSettings).GetField("_options", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("StationSettings options field was not found.");
            object? configuredOptions = optionsField.GetValue(null);

            try
            {
                optionsField.SetValue(null, null);
                Assert.ThrowsExactly<InvalidOperationException>(() => _ = StationSettings.TelemetryPort);
            }
            finally
            {
                optionsField.SetValue(null, configuredOptions);
            }
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
