using System;
using System.Runtime.InteropServices;

namespace RoverRally.Core.Session
{
    /// <summary>
    /// One completed run, as stored in the station's binary session cache.
    /// The layout mirrors the RL_SESSION block in the RL-100 integration
    /// manual, including the reserved pointer slot the SDK keeps at the end.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SessionCacheRecord
    {
        public int RoverId;

        /// <summary>
        /// Opaque session handle returned by the SDK. It is kept in the record
        /// so the block round-trips unchanged through RL_SESSION.
        /// </summary>
        public IntPtr SessionHandle;

        public long StartedUtcTicks;
        public long EndedUtcTicks;
        public int DistanceCm;
        public int PeakSpeedCmS;

        public DateTime StartedUtc
        {
            get { return FromTicks(StartedUtcTicks); }
        }

        public DateTime EndedUtc
        {
            get { return FromTicks(EndedUtcTicks); }
        }

        private static DateTime FromTicks(long ticks)
        {
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return DateTime.MinValue;
            }

            return new DateTime(ticks, DateTimeKind.Utc);
        }

        public TimeSpan Duration
        {
            get { return EndedUtc - StartedUtc; }
        }
    }
}
