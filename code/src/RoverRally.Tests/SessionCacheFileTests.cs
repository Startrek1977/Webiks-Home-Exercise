using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Session;
using RoverRally.Tests.TestSupport;

namespace RoverRally.Tests
{
    [TestClass]
    public class SessionCacheFileTests
    {
        /// <summary>
        /// The real bytes of RoverRally.App/Data/session-cache.bin, copied
        /// verbatim (not reconstructed from the struct) so this test catches a
        /// transcription error the same way FrameCodecTests' golden frames do.
        /// Bytes 4-7 of every 32-byte record are the reserved gap where the
        /// vendor SDK's IntPtr session handle used to sit; SessionCacheFile
        /// must ignore them on read and write them as zero.
        /// </summary>
        private static readonly byte[] GoldenShippedSessionCache = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x60, 0x91, 0xFD, 0xAE, 0x01, 0xDF, 0x08,
            0x00, 0x4C, 0x4C, 0x81, 0xB1, 0x01, 0xDF, 0x08, 0x1C, 0x45, 0x03, 0x00, 0x9C, 0x01, 0x00, 0x00,
            0x03, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0xBA, 0x3C, 0x7F, 0xBE, 0x01, 0xDF, 0x08,
            0x00, 0x90, 0x4E, 0xFD, 0xC1, 0x01, 0xDF, 0x08, 0xDE, 0xCF, 0x01, 0x00, 0xF6, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x90, 0xAC, 0xCD, 0x74, 0x02, 0xDF, 0x08,
            0x00, 0x50, 0x15, 0x46, 0x79, 0x02, 0xDF, 0x08, 0x54, 0xE7, 0x05, 0x00, 0x5D, 0x02, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xF0, 0x26, 0xCD, 0x9A, 0x02, 0xDF, 0x08,
            0x00, 0xC4, 0xD4, 0xC1, 0x9C, 0x02, 0xDF, 0x08, 0x50, 0x53, 0x02, 0x00, 0x3B, 0x02, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00, 0xF6, 0x0D, 0x58, 0x40, 0x03, 0xDF, 0x08,
            0x00, 0xB4, 0x12, 0x47, 0x43, 0x03, 0xDF, 0x08, 0x24, 0xC9, 0x03, 0x00, 0xB1, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00, 0x3E, 0x1E, 0xAA, 0x77, 0x03, 0xDF, 0x08,
            0x00, 0xB4, 0xFB, 0xEB, 0x78, 0x03, 0xDF, 0x08, 0x10, 0xEF, 0x00, 0x00, 0xC6, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x70, 0x00, 0x00, 0x00, 0xFA, 0xD6, 0x1C, 0x08, 0x04, 0xDF, 0x08,
            0x00, 0x18, 0x10, 0x48, 0x0D, 0x04, 0xDF, 0x08, 0xA2, 0xBB, 0x06, 0x00, 0x6A, 0x02, 0x00, 0x00,
            0x03, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0xB4, 0xB7, 0x8C, 0xDE, 0x04, 0xDF, 0x08,
            0x00, 0x5C, 0x13, 0x76, 0xE2, 0x04, 0xDF, 0x08, 0xE0, 0x09, 0x02, 0x00, 0xFC, 0x00, 0x00, 0x00
        };

        private static readonly int[] ExpectedRoverIds = { 1, 3, 2, 4, 1, 5, 2, 3 };
        private static readonly long[] ExpectedStartedTicks =
        {
            639231523200000000, 639231589800000000, 639232372800000000, 639232536000000000,
            639233247000000000, 639233484600000000, 639234105000000000, 639235026000000000
        };
        private static readonly long[] ExpectedEndedTicks =
        {
            639231534000000000, 639231604800000000, 639232392000000000, 639232544400000000,
            639233259600000000, 639233490000000000, 639234127200000000, 639235042800000000
        };
        private static readonly int[] ExpectedDistanceCm = { 214300, 118750, 386900, 152400, 248100, 61200, 441250, 133600 };
        private static readonly int[] ExpectedPeakSpeedCmS = { 412, 246, 605, 571, 433, 198, 618, 252 };

        private static string TempCachePath()
        {
            return Path.Combine(Path.GetTempPath(), "session-cache-" + Guid.NewGuid().ToString("N") + ".bin");
        }

        [TestMethod]
        public void AppendThenReadRoundTripsEveryField()
        {
            SessionCacheRecord written = new SessionCacheRecord();
            written.RoverId = 42;
            written.StartedUtcTicks = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).Ticks;
            written.EndedUtcTicks = new DateTime(2026, 1, 2, 3, 44, 5, DateTimeKind.Utc).Ticks;
            written.DistanceCm = 987654;
            written.PeakSpeedCmS = 555;

            string path = TempCachePath();
            try
            {
                SessionCacheFile.Append(path, written);
                IList<SessionCacheRecord> readBack = SessionCacheFile.Read(path);

                Assert.AreEqual(1, readBack.Count);
                SessionCacheRecord record = readBack[0];
                Assert.AreEqual(written.RoverId, record.RoverId);
                Assert.AreEqual(written.StartedUtcTicks, record.StartedUtcTicks);
                Assert.AreEqual(written.EndedUtcTicks, record.EndedUtcTicks);
                Assert.AreEqual(written.DistanceCm, record.DistanceCm);
                Assert.AreEqual(written.PeakSpeedCmS, record.PeakSpeedCmS);
                Assert.AreEqual(written.StartedUtc, record.StartedUtc);
                Assert.AreEqual(written.EndedUtc, record.EndedUtc);
                Assert.AreEqual(written.Duration, record.Duration);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void AppendWritesThirtyTwoBytesPerRecordRegardlessOfArchitecture()
        {
            string path = TempCachePath();
            try
            {
                SessionCacheFile.Append(path, new SessionCacheRecord());
                SessionCacheFile.Append(path, new SessionCacheRecord());

                Assert.AreEqual(64L, new FileInfo(path).Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void AppendWritesTheReservedGapAsZero()
        {
            // Bytes 4-7 of every record are the reserved slot where the vendor
            // SDK's IntPtr session handle used to live; nothing should ever
            // write anything there again.
            SessionCacheRecord record = new SessionCacheRecord();
            record.RoverId = -1;
            record.StartedUtcTicks = -1;
            record.EndedUtcTicks = -1;
            record.DistanceCm = -1;
            record.PeakSpeedCmS = -1;

            string path = TempCachePath();
            try
            {
                SessionCacheFile.Append(path, record);
                byte[] raw = File.ReadAllBytes(path);

                Assert.AreEqual((byte)0, raw[4]);
                Assert.AreEqual((byte)0, raw[5]);
                Assert.AreEqual((byte)0, raw[6]);
                Assert.AreEqual((byte)0, raw[7]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void StartedUtcClampsToMinValueForOutOfRangeTicks()
        {
            // FromTicks guards ticks outside DateTime's valid range so a
            // corrupt or pre-epoch record can't throw when its history is
            // just being displayed.
            SessionCacheRecord record = new SessionCacheRecord();
            record.StartedUtcTicks = long.MinValue;

            Assert.AreEqual(DateTime.MinValue, record.StartedUtc);
        }

        [TestMethod]
        public void EndedUtcClampsToMinValueForOutOfRangeTicks()
        {
            SessionCacheRecord record = new SessionCacheRecord();
            record.EndedUtcTicks = long.MaxValue;

            Assert.AreEqual(DateTime.MinValue, record.EndedUtc);
        }

        [TestMethod]
        public void StartedUtcDoesNotClampAtTheLowestValidTicksValue()
        {
            // ticks == 0 is DateTime.MinValue.Ticks, the guard's inclusive lower
            // bound - it must take the constructed path, not the clamp path.
            // DateTime equality ignores Kind, and DateTime.MinValue's value
            // happens to equal new DateTime(0, Utc)'s value, so the Kind check
            // is what actually tells the two branches apart here.
            SessionCacheRecord record = new SessionCacheRecord();
            record.StartedUtcTicks = 0;

            Assert.AreEqual(DateTime.MinValue, record.StartedUtc);
            Assert.AreEqual(DateTimeKind.Utc, record.StartedUtc.Kind);
        }

        [TestMethod]
        public void EndedUtcDoesNotClampAtTheHighestValidTicksValue()
        {
            SessionCacheRecord record = new SessionCacheRecord();
            record.EndedUtcTicks = DateTime.MaxValue.Ticks;

            Assert.AreEqual(DateTime.MaxValue, record.EndedUtc);
        }

        [TestMethod]
        public void DurationIsNegativeWhenOnlyTheEndedTicksAreOutOfRange()
        {
            // FromTicks never throws on corrupt data, so a bad EndedUtcTicks
            // silently produces a large negative Duration rather than an
            // error - documenting that consequence here.
            SessionCacheRecord record = new SessionCacheRecord();
            DateTime started = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            record.StartedUtcTicks = started.Ticks;
            record.EndedUtcTicks = long.MaxValue;

            Assert.AreEqual(TimeSpan.FromTicks(-started.Ticks), record.Duration);
        }

        [TestMethod]
        public void ReadReturnsEmptyListWhenFileIsMissing()
        {
            IList<SessionCacheRecord> records = SessionCacheFile.Read(TempCachePath());

            Assert.AreEqual(0, records.Count);
        }

        [TestMethod]
        public void ReadsTheRealShippedSessionCacheAsEightRecordsWithUnchangedValues()
        {
            string path = TempCachePath();
            try
            {
                File.WriteAllBytes(path, GoldenShippedSessionCache);
                IList<SessionCacheRecord> records = SessionCacheFile.Read(path);

                Assert.AreEqual(8, records.Count);
                for (int i = 0; i < records.Count; i++)
                {
                    SessionCacheRecord record = records[i];
                    Assert.AreEqual(ExpectedRoverIds[i], record.RoverId, "RoverId mismatch at record " + i);
                    Assert.AreEqual(ExpectedStartedTicks[i], record.StartedUtcTicks, "StartedUtcTicks mismatch at record " + i);
                    Assert.AreEqual(ExpectedEndedTicks[i], record.EndedUtcTicks, "EndedUtcTicks mismatch at record " + i);
                    Assert.AreEqual(ExpectedDistanceCm[i], record.DistanceCm, "DistanceCm mismatch at record " + i);
                    Assert.AreEqual(ExpectedPeakSpeedCmS[i], record.PeakSpeedCmS, "PeakSpeedCmS mismatch at record " + i);
                    Assert.IsTrue(record.EndedUtc >= record.StartedUtc, "Run ended before it started at record " + i);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ReadLogsAWarningForATruncatedFileViaTheInjectedLogger()
        {
            string path = TempCachePath();
            try
            {
                byte[] truncated = GoldenShippedSessionCache.Take(SessionCacheFile.RecordSize + 5).ToArray();
                File.WriteAllBytes(path, truncated);
                CapturingLogger logger = new CapturingLogger();

                SessionCacheFile.Read(path, logger);

                Assert.IsTrue(logger.HasEntry(LogLevel.Warning, "truncated or corrupted"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ReadLogsInformationWhenTheFileIsMissingViaTheInjectedLogger()
        {
            CapturingLogger logger = new CapturingLogger();

            SessionCacheFile.Read(TempCachePath(), logger);

            Assert.IsTrue(logger.HasEntry(LogLevel.Information, "starting empty"));
        }
    }
}
