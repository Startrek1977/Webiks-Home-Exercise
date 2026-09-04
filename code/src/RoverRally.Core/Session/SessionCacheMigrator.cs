using System.IO;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Session
{
    /// <summary>
    /// One-time converter for <c>session-cache.bin</c> files left behind by the
    /// old x86 station. The legacy layout and the current one share the same
    /// 32-byte stride - <see cref="SessionCacheFile"/> already reads either one
    /// correctly - but a legacy file's reserved slot (offset 4-7 of every
    /// record) still holds the vendor SDK's real <c>IntPtr</c> session handle
    /// instead of the zero that <see cref="SessionCacheFile.Append"/> always
    /// writes there. That is the only byte-level difference between the two
    /// formats, so migrating means zeroing that slot in every record, after
    /// preserving the original.
    ///
    /// This is deliberately stricter than <see cref="SessionCacheFile.Read"/>:
    /// a run-time read tolerates and logs a truncated trailing record because
    /// the station still has to start, but a one-time migration has no such
    /// excuse and refuses to guess at a corrupt or truncated file instead of
    /// producing a garbage record.
    ///
    /// The offsets below were verified against the real shipped
    /// <c>RoverRally.App/Data/session-cache.bin</c> (256 bytes, 8 records).
    ///
    /// The backup-and-write step is one atomic <see cref="File.Replace(string, string, string)"/>
    /// call rather than a copy followed by an in-place write, so a process
    /// interrupted mid-write (disk full, power loss, AV lock) leaves either the
    /// untouched original or the fully-converted file - never a half-written one.
    /// </summary>
    public static class SessionCacheMigrator
    {
        private const string BackupSuffix = ".legacy";

        private const int SessionHandleOffset = 4;
        private const int SessionHandleSize = 4;

        /// <summary>
        /// Migrates the session cache at <paramref name="path"/> in place if it
        /// still carries legacy session-handle bytes. Returns <c>false</c> and
        /// leaves the file untouched if it is missing or already migrated.
        /// Throws <see cref="IOException"/>, leaving the file untouched, if the
        /// file is corrupt/truncated or if a backup from a previous run is
        /// already present.
        /// </summary>
        public static bool MigrateIfNeeded(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            byte[] raw = File.ReadAllBytes(path);
            int recordSize = SessionCacheFile.RecordSize;

            if (raw.Length % recordSize != 0)
            {
                throw new IOException(string.Format(
                    "Session cache at {0} is {1} byte(s), not a whole number of {2}-byte records; "
                    + "refusing to migrate a truncated or corrupted file.",
                    path, raw.Length, recordSize));
            }

            int count = raw.Length / recordSize;

            if (IsAlreadyMigrated(raw, count, recordSize))
            {
                Log.Info("Session cache at " + path + " has no legacy session-handle bytes left; nothing to migrate.");
                return false;
            }

            string backupPath = path + BackupSuffix;
            if (File.Exists(backupPath))
            {
                throw new IOException(string.Format(
                    "A backup already exists at {0}; refusing to overwrite it. "
                    + "Remove or move it aside before migrating again.",
                    backupPath));
            }

            byte[] converted = (byte[])raw.Clone();
            ZeroSessionHandles(converted, count, recordSize);

            // Write the converted bytes to a temp file first and swap it into
            // place with File.Replace, which performs the backup-and-replace as
            // one atomic filesystem operation. Writing straight to `path` risks
            // leaving a half-written session-cache.bin behind if the process is
            // interrupted mid-write (disk full, power loss, AV lock) - and since
            // the backup would already exist, a later run would refuse to retry.
            string tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, converted);
            try
            {
                File.Replace(tempPath, path, backupPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }

            Log.Info(string.Format(
                "Migrated {0} legacy session cache record(s) at {1}; original preserved at {2}.",
                count, path, backupPath));
            return true;
        }

        private static bool IsAlreadyMigrated(byte[] raw, int count, int recordSize)
        {
            for (int i = 0; i < count; i++)
            {
                int offset = (i * recordSize) + SessionHandleOffset;
                for (int b = 0; b < SessionHandleSize; b++)
                {
                    if (raw[offset + b] != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ZeroSessionHandles(byte[] raw, int count, int recordSize)
        {
            for (int i = 0; i < count; i++)
            {
                int offset = (i * recordSize) + SessionHandleOffset;
                for (int b = 0; b < SessionHandleSize; b++)
                {
                    raw[offset + b] = 0;
                }
            }
        }
    }
}
