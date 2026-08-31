using RoverRally.Core.Models;

namespace RoverRally.Core.Geo
{
    /// <summary>
    /// Watches a rover against the fenced area of the proving ground and
    /// reports when it has left. A sample of hysteresis keeps a single noisy
    /// fix from tripping the alarm every time the receiver wobbles.
    /// </summary>
    public class GeofenceMonitor
    {
        private readonly TrackPoint[] _fence;
        private TrackPoint _lastEvaluated;
        private bool _hasSample;

        public GeofenceMonitor(TrackPoint[] fence)
        {
            _fence = fence;
        }

        public bool IsOutside(TrackPoint position)
        {
            bool outside = _hasSample && !Contains(_lastEvaluated);

            _lastEvaluated = position;
            _hasSample = true;

            return outside;
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
