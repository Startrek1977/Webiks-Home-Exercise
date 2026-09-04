using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Session;

namespace RoverRally.Tests
{
    [TestClass]
    public class SessionCacheMigratorTests
    {
        /// <summary>
        /// The real bytes of RoverRally.App/Data/session-cache.bin - the same
        /// fixture used by SessionCacheFileTests - copied verbatim so this
        /// suite exercises the migrator against genuine legacy bytes (offset
        /// 4-7 of every record still holds a real, non-zero SessionHandle),
        /// not a hand-built approximation.
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
            return Path.Combine(Path.GetTempPath(), "session-cache-migrator-" + Guid.NewGuid().ToString("N") + ".bin");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void MigratesTheRealShippedBytesAndPreservesTheOriginalAsABackup()
        {
            string path = TempCachePath();
            string backupPath = path + ".legacy";
            try
            {
                File.WriteAllBytes(path, GoldenShippedSessionCache);

                bool migrated = SessionCacheMigrator.MigrateIfNeeded(path);

                Assert.IsTrue(migrated);

                Assert.IsTrue(File.Exists(backupPath));
                CollectionAssert.AreEqual(GoldenShippedSessionCache, File.ReadAllBytes(backupPath));

                byte[] converted = File.ReadAllBytes(path);
                for (int i = 0; i < 8; i++)
                {
                    int reservedOffset = (i * SessionCacheFile.RecordSize) + 4;
                    Assert.AreEqual((byte)0, converted[reservedOffset], "record " + i);
                    Assert.AreEqual((byte)0, converted[reservedOffset + 1], "record " + i);
                    Assert.AreEqual((byte)0, converted[reservedOffset + 2], "record " + i);
                    Assert.AreEqual((byte)0, converted[reservedOffset + 3], "record " + i);
                }

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
                }
            }
            finally
            {
                DeleteIfExists(path);
                DeleteIfExists(backupPath);
            }
        }

        [TestMethod]
        public void ReturnsFalseAndWritesNothingWhenAlreadyMigrated()
        {
            string path = TempCachePath();
            string backupPath = path + ".legacy";
            try
            {
                SessionCacheRecord first = new SessionCacheRecord();
                first.RoverId = 1;
                first.DistanceCm = 100;
                SessionCacheFile.Append(path, first);

                SessionCacheRecord second = new SessionCacheRecord();
                second.RoverId = 2;
                second.DistanceCm = 200;
                SessionCacheFile.Append(path, second);
                byte[] before = File.ReadAllBytes(path);

                bool migrated = SessionCacheMigrator.MigrateIfNeeded(path);

                Assert.IsFalse(migrated);
                Assert.IsFalse(File.Exists(backupPath));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            }
            finally
            {
                DeleteIfExists(path);
                DeleteIfExists(backupPath);
            }
        }

        [TestMethod]
        public void ReturnsFalseWhenFileIsMissing()
        {
            bool migrated = SessionCacheMigrator.MigrateIfNeeded(TempCachePath());

            Assert.IsFalse(migrated);
        }

        [TestMethod]
        public void ThrowsAndLeavesTheFileUntouchedWhenLengthIsNotAWholeNumberOfRecords()
        {
            string path = TempCachePath();
            string backupPath = path + ".legacy";
            try
            {
                byte[] truncated = GoldenShippedSessionCache.Take(SessionCacheFile.RecordSize + 5).ToArray();
                File.WriteAllBytes(path, truncated);

                Assert.ThrowsExactly<IOException>(() => SessionCacheMigrator.MigrateIfNeeded(path));

                Assert.IsFalse(File.Exists(backupPath));
                CollectionAssert.AreEqual(truncated, File.ReadAllBytes(path));
            }
            finally
            {
                DeleteIfExists(path);
                DeleteIfExists(backupPath);
            }
        }

        [TestMethod]
        public void ThrowsWhenABackupAlreadyExists()
        {
            string path = TempCachePath();
            string backupPath = path + ".legacy";
            byte[] priorBackupContent = { 1, 2, 3, 4 };
            try
            {
                File.WriteAllBytes(path, GoldenShippedSessionCache);
                File.WriteAllBytes(backupPath, priorBackupContent);

                Assert.ThrowsExactly<IOException>(() => SessionCacheMigrator.MigrateIfNeeded(path));

                CollectionAssert.AreEqual(priorBackupContent, File.ReadAllBytes(backupPath));
                CollectionAssert.AreEqual(GoldenShippedSessionCache, File.ReadAllBytes(path));
            }
            finally
            {
                DeleteIfExists(path);
                DeleteIfExists(backupPath);
            }
        }
    }
}
