using RoverRally.Core.Models;

namespace RoverRally.Core.Geo
{
    /// <summary>
    /// Watches the fleet against the fenced area of the proving ground and
    /// reports when a given rover has left it.
    ///
    /// No hysteresis: each call reports the fence state of the position it is
    /// given, immediately. An earlier version lagged one sample as a stand-in
    /// for hysteresis, using a single field shared by every caller - which,
    /// with one monitor serving the whole fleet, meant each rover's verdict
    /// was actually the previous rover's position (#4). A one-sample lag was
    /// never real hysteresis anyway (that needs a consecutive-sample
    /// threshold or separate enter/exit boundaries, not a stale field), and
    /// smoothing a single noisy fix is not worth delaying a fence alert for -
    /// this is a safety warning, and a late-but-smooth verdict is worse than
    /// an immediate one. Getting rid of the lag also gets rid of the state
    /// that could be shared across rovers by mistake: there is nothing left
    /// here to mix up between vehicles.
    /// </summary>
    public class GeofenceMonitor
    {
        private readonly TrackPoint[] _fence;

        public GeofenceMonitor(TrackPoint[] fence)
        {
            _fence = fence;
        }

        /// <summary>
        /// True when <paramref name="position"/> is outside the fence, right
        /// now - not the previous call's position, for this rover or any
        /// other. <paramref name="roverId"/> does not affect the answer; it
        /// stays part of the signature so a call site can never accidentally
        /// check one rover's position while attributing the verdict to
        /// another, and so a real per-rover hysteresis scheme could be added
        /// later without changing every caller.
        /// </summary>
        public bool IsOutside(byte roverId, TrackPoint position)
        {
            return !Contains(position);
        }

        public bool Contains(TrackPoint point)
        {
            bool inside = false;

            for (int i = 0, j = _fence.Length - 1; i < _fence.Length; j = i++)
            {
                bool straddles = (_fence[i].Latitude > point.Latitude) != (_fence[j].Latitude > point.Latitude);
                if (!straddles) continue;

                double crossing = (_fence[j].Longitude - _fence[i].Longitude)
                                  * (point.Latitude - _fence[i].Latitude)
                                  / (_fence[j].Latitude - _fence[i].Latitude)
                                  + _fence[i].Longitude;

                if (point.Longitude < crossing) inside = !inside;
            }

            return inside;
        }
    }
}
