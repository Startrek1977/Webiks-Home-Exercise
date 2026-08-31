using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Session
{
    /// <summary>
    /// Fixed stride binary store for completed runs. It is written once at the
    /// end of every run and read back when the fleet grid is populated, so a
    /// flat file beats carrying a database engine around the site.
    /// </summary>
    public static class SessionCacheFile
    {
        private static readonly int RecordSize = Marshal.SizeOf(typeof(SessionCacheRecord));

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

            IntPtr scratch = Marshal.AllocHGlobal(RecordSize);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Marshal.Copy(raw, i * RecordSize, scratch, RecordSize);
                    records.Add((SessionCacheRecord)Marshal.PtrToStructure(scratch, typeof(SessionCacheRecord)));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(scratch);
            }

            Log.Info(string.Format("Loaded {0} run(s) from the session cache.", records.Count));
            return records;
        }

        public static void Append(string path, SessionCacheRecord record)
        {
            byte[] buffer = new byte[RecordSize];

            IntPtr scratch = Marshal.AllocHGlobal(RecordSize);
            try
            {
                Marshal.StructureToPtr(record, scratch, false);
                Marshal.Copy(scratch, buffer, 0, RecordSize);
            }
            finally
            {
                Marshal.FreeHGlobal(scratch);
            }

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
