using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Export;
using RoverRally.Core.Session;

namespace RoverRally.Tests
{
    [TestClass]
    public class RunHistoryCsvExporterTests
    {
        private static readonly string HeaderRow = "RoverId,RoverName,StartedUtc,EndedUtc,DurationSeconds,DistanceMeters,PeakSpeedKmh";

        private static SessionCacheRecord MakeRecord(int roverId, DateTime startedUtc, DateTime endedUtc, int distanceCm, int peakSpeedCmS)
        {
            return new SessionCacheRecord
            {
                RoverId = roverId,
                StartedUtcTicks = startedUtc.Ticks,
                EndedUtcTicks = endedUtc.Ticks,
                DistanceCm = distanceCm,
                PeakSpeedCmS = peakSpeedCmS
            };
        }

        [TestMethod]
        public void AnEmptyRunListProducesOnlyTheHeaderRow()
        {
            string csv = RunHistoryCsvExporter.BuildCsv(new List<SessionCacheRecord>(), id => "Rover " + id);

            Assert.AreEqual(HeaderRow + "\r\n", csv);
        }

        [TestMethod]
        public void FormatsARecordWithInvariantNumbersAndIso8601Timestamps()
        {
            DateTime started = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc);
            DateTime ended = new DateTime(2026, 3, 5, 9, 2, 30, DateTimeKind.Utc);
            SessionCacheRecord record = MakeRecord(7, started, ended, distanceCm: 1250, peakSpeedCmS: 500);

            string csv = RunHistoryCsvExporter.BuildCsv(new[] { record }, id => "Scout");

            string expectedRow = "7,\"Scout\",2026-03-05T09:00:00Z,2026-03-05T09:02:30Z,150,12.5,18.0";
            Assert.AreEqual(HeaderRow + "\r\n" + expectedRow + "\r\n", csv);
        }

        [TestMethod]
        public void QuotesAndEscapesARoverNameContainingCommasAndQuotes()
        {
            SessionCacheRecord record = MakeRecord(1, DateTime.UtcNow, DateTime.UtcNow, 0, 0);

            string csv = RunHistoryCsvExporter.BuildCsv(new[] { record }, id => "O\"Brien, Ch. 2");

            StringAssert.Contains(csv, "\"O\"\"Brien, Ch. 2\"");
        }

        [DataTestMethod]
        [DataRow("=SUM(1,1)")]
        [DataRow("+SUM(1,1)")]
        [DataRow("-SUM(1,1)")]
        [DataRow("@SUM(1,1)")]
        [DataRow("\tSUM(1,1)")]
        [DataRow("\rSUM(1,1)")]
        public void PrefixesSpreadsheetFormulaTriggersWithASingleQuote(string roverName)
        {
            SessionCacheRecord record = MakeRecord(1, DateTime.UtcNow, DateTime.UtcNow, 0, 0);

            string csv = RunHistoryCsvExporter.BuildCsv(new[] { record }, id => roverName);

            StringAssert.Contains(csv, "\"'" + roverName.Replace("\"", "\"\"") + "\"");
        }

        [TestMethod]
        public void StaysInvariantEvenUnderACommaDecimalCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            try
            {
                DateTime started = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime ended = started.AddSeconds(1.5);
                SessionCacheRecord record = MakeRecord(1, started, ended, distanceCm: 250, peakSpeedCmS: 100);

                string csv = RunHistoryCsvExporter.BuildCsv(new[] { record }, id => "Rover");

                StringAssert.Contains(csv, "1.5,2.5,3.6");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void ARecordWithCorruptTicksFormatsWithoutThrowing()
        {
            SessionCacheRecord record = new SessionCacheRecord
            {
                RoverId = 3,
                StartedUtcTicks = long.MaxValue,
                EndedUtcTicks = long.MaxValue,
                DistanceCm = 0,
                PeakSpeedCmS = 0
            };

            string csv = RunHistoryCsvExporter.BuildCsv(new[] { record }, id => "Rover " + id);

            StringAssert.Contains(csv, "0001-01-01T00:00:00Z");
        }
    }
}
