using System;

namespace RoverRally.Core.Models
{
    /// <summary>
    /// A position on the proving ground, in decimal degrees.
    /// </summary>
    public struct TrackPoint
    {
        public double Latitude;
        public double Longitude;

        public TrackPoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        /// <summary>
        /// True when the position carries no usable fix. The RL-100 sends
        /// zeroed coordinates while the receiver is still acquiring satellites.
        /// </summary>
        public bool IsEmpty
        {
            get { return Math.Abs(Latitude) < 0.000001 && Math.Abs(Longitude) < 0.000001; }
        }

        public override string ToString()
        {
            return string.Format("{0:0.000000}, {1:0.000000}", Latitude, Longitude);
        }
    }
}
