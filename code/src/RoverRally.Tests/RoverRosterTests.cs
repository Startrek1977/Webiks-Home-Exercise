using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Models;
using RoverRally.Core.Roster;

namespace RoverRally.Tests
{
    /// <summary>
    /// RoverRoster.Load is the only place in the solution that parses JSON,
    /// and the roster file is one a site lead edits by hand rather than a
    /// developer, so a bad file must fail into an empty list - never throw
    /// out of Load - and say so in the log.
    /// </summary>
    [TestClass]
    public class RoverRosterTests
    {
        private static string TempRosterPath()
        {
            return Path.Combine(Path.GetTempPath(), "roster-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        [TestMethod]
        public void LoadsEveryRoverFieldFromAWellFormedRosterFile()
        {
            string path = TempRosterPath();
            File.WriteAllText(path, @"[
              { ""Id"": 1, ""Name"": ""Falafel"", ""ChassisType"": ""RR-4 Rock Crawler"", ""RadioSerial"": ""RL100-0417"" },
              { ""Id"": 2, ""Name"": ""Dune Goblin"", ""ChassisType"": ""RR-6 Dune Buggy"", ""RadioSerial"": ""RL100-0388"" }
            ]");

            try
            {
                IList<Rover> rovers = RoverRoster.Load(path);

                Assert.AreEqual(2, rovers.Count);
                Assert.AreEqual((byte)1, rovers[0].Id);
                Assert.AreEqual("Falafel", rovers[0].Name);
                Assert.AreEqual("RR-4 Rock Crawler", rovers[0].ChassisType);
                Assert.AreEqual("RL100-0417", rovers[0].RadioSerial);
                Assert.AreEqual((byte)2, rovers[1].Id);
                Assert.AreEqual("Dune Goblin", rovers[1].Name);
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [TestMethod]
        public void ReturnsAnEmptyListWhenTheRosterFileDoesNotExist()
        {
            string path = TempRosterPath();
            DeleteIfExists(path);

            IList<Rover> rovers = RoverRoster.Load(path);

            Assert.AreEqual(0, rovers.Count);
        }

        [TestMethod]
        public void ReturnsAnEmptyListInsteadOfThrowingForMalformedJson()
        {
            string path = TempRosterPath();
            File.WriteAllText(path, "{ this is not valid JSON ]");

            try
            {
                IList<Rover> rovers = RoverRoster.Load(path);

                Assert.AreEqual(0, rovers.Count);
            }
            finally
            {
                DeleteIfExists(path);
            }
        }

        [TestMethod]
        public void ReturnsAnEmptyListForAFileContainingTheJsonLiteralNull()
        {
            string path = TempRosterPath();
            File.WriteAllText(path, "null");

            try
            {
                IList<Rover> rovers = RoverRoster.Load(path);

                Assert.AreEqual(0, rovers.Count);
            }
            finally
            {
                DeleteIfExists(path);
            }
        }
    }
}
