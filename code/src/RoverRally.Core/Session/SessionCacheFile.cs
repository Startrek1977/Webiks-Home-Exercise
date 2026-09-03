using System;
using System.Collections.Generic;
using System.IO;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Session
{
    /// <summary>
    /// Fixed stride binary store for completed runs. It is written once at the
    /// end of every run and read back when the fleet grid is populated, so a
    /// flat file beats carrying a database engine around the site.
    ///
    /// The stride is a fixed set of byte offsets, not a marshalled struct size:
    /// a value that depends on <c>Marshal.SizeOf</c> changes with process
    /// architecture (4 vs 8 byte pointers, different padding), and the on-disk
    /// format must not. Offset 4 is reserved - it used to hold the vendor SDK's
    /// session handle - and is written as zero and ignored on read, so the
    /// shipped session-cache.bin keeps reading unchanged.
    /// </summary>
    public static class SessionCacheFile
    {
        public const int RecordSize = 32;

        private const int RoverIdOffset = 0;
        private const int ReservedOffset = 4;
        private const int StartedUtcTicksOffset = 8;
        private const int EndedUtcTicksOffset = 16;
        private const int DistanceCmOffset = 24;
        private const int PeakSpeedCmSOffset = 28;

        public static IList<SessionCacheRecord> Read(string path)
        {
            List<SessionCacheRecord> records = new List<SessionCacheRecord>();

            if (!File.Exists(path))
            {
                Log.Info("No session cache at " + path + ", starting empty.");
                return records;
            }

            byte[] raw = File.ReadAllBytes(path);
            int count = raw.Length / RecordSize;

            for (int i = 0; i < count; i++)
            {
                int offset = i * RecordSize;

                SessionCacheRecord record = new SessionCacheRecord();
                record.RoverId = BitConverter.ToInt32(raw, offset + RoverIdOffset);
                record.StartedUtcTicks = BitConverter.ToInt64(raw, offset + StartedUtcTicksOffset);
                record.EndedUtcTicks = BitConverter.ToInt64(raw, offset + EndedUtcTicksOffset);
                record.DistanceCm = BitConverter.ToInt32(raw, offset + DistanceCmOffset);
                record.PeakSpeedCmS = BitConverter.ToInt32(raw, offset + PeakSpeedCmSOffset);
                records.Add(record);
            }

            Log.Info(string.Format("Loaded {0} run(s) from the session cache.", records.Count));
            return records;
        }

        public static void Append(string path, SessionCacheRecord record)
        {
            byte[] buffer = new byte[RecordSize];

            Buffer.BlockCopy(BitConverter.GetBytes(record.RoverId), 0, buffer, RoverIdOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(record.StartedUtcTicks), 0, buffer, StartedUtcTicksOffset, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(record.EndedUtcTicks), 0, buffer, EndedUtcTicksOffset, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(record.DistanceCm), 0, buffer, DistanceCmOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(record.PeakSpeedCmS), 0, buffer, PeakSpeedCmSOffset, 4);
            // ReservedOffset is left as zero.

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
