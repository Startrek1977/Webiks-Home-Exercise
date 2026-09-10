using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RoverRally.App.Views
{
    /// <summary>
    /// Tracks one rover's breadcrumb trail as a sequence of disconnected point
    /// segments, so a gap in reporting - most commonly a rover driving off the
    /// surveyed track extent and back - starts a new segment instead of the
    /// next on-map fix silently connecting across the gap with a straight
    /// line (#80). Pure point bookkeeping with no WPF Shape/Canvas dependency,
    /// so it's directly unit-testable; TrackView owns turning each segment
    /// into a Polyline.
    /// </summary>
    public class RoverTrail
    {
        private readonly int _maxPoints;
        private readonly List<List<Point>> _segments = new List<List<Point>>();
        private bool _hasGap;

        public RoverTrail(int maxPoints)
        {
            _maxPoints = maxPoints;
        }

        public IReadOnlyList<IReadOnlyList<Point>> Segments => _segments;

        /// <summary>
        /// Records that a fix was skipped - the rover could not be placed on
        /// the map - so the next successful AddPoint starts a new segment
        /// instead of continuing the last one.
        /// </summary>
        public void RecordGap()
        {
            _hasGap = true;
        }

        /// <summary>
        /// Appends a fix. Starts a new segment if a gap was recorded since
        /// the last point (or this is the very first point ever), otherwise
        /// continues the current segment. Trims the oldest points, across
        /// segments if necessary, once the total exceeds the configured cap.
        /// </summary>
        public void AddPoint(double x, double y)
        {
            if (_hasGap || _segments.Count == 0)
            {
                _segments.Add(new List<Point>());
            }

            _hasGap = false;

            _segments[_segments.Count - 1].Add(new Point(x, y));
            Trim();
        }

        private void Trim()
        {
            int total = _segments.Sum(segment => segment.Count);

            while (total > _maxPoints)
            {
                _segments[0].RemoveAt(0);
                total--;

                if (_segments[0].Count == 0)
                {
                    _segments.RemoveAt(0);
                }
            }
        }
    }
}
