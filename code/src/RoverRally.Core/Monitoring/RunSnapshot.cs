using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RoverRally.Core.Monitoring
{
    /// <summary>
    /// A point in time copy of the fleet, handed to the office overview client
    /// so it can render the same picture the station shows.
    /// </summary>
    [Serializable]
    public class RunSnapshot
    {
        public DateTime CapturedUtc { get; set; }
        public string StationName { get; set; }
        public List<RunSnapshotEntry> Entries { get; set; }

        public RunSnapshot()
        {
            Entries = new List<RunSnapshotEntry>();
        }

        public byte[] Serialize()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, this);
                return stream.ToArray();
            }
        }

        public static RunSnapshot Deserialize(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return (RunSnapshot)formatter.Deserialize(stream);
            }
        }
    }

    [Serializable]
    public class RunSnapshotEntry
    {
        public int RoverId { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int SpeedCmS { get; set; }
        public int BatteryMilliVolts { get; set; }
        public string Status { get; set; }
    }
}
