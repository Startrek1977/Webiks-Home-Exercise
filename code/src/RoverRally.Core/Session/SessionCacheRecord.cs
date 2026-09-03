using System;

namespace RoverRally.Core.Session
{
    /// <summary>
    /// One completed run, as stored in the station's binary session cache.
    /// The on-disk layout is fixed-width and architecture-independent - see
    /// <see cref="SessionCacheFile.RecordSize"/> - and keeps a 4-byte
    /// reserved gap where the vendor SDK's session handle used to sit, so the
    /// existing session-cache.bin does not need to change shape.
    /// </summary>
    public struct SessionCacheRecord
    {
        public int RoverId;

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
