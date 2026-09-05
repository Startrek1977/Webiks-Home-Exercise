using System;
using RoverRally.Core.Models;

namespace RoverRally.Core.Control
{
    /// <summary>
    /// Counts laps for one rover by watching the segment between consecutive
    /// position fixes for a crossing of the start line, rather than how close
    /// a single fix comes to it - telemetry arrives at about 5 Hz, so a
    /// vehicle can move a noticeable distance between fixes, and the crossing
    /// can fall anywhere along that gap.
    ///
    /// Only a crossing in the racing direction counts, so a rover reversing
    /// back across the line - or oscillating across it without making net
    /// progress - does not inflate the count. Direction is fixed by the order
    /// of the two points this instance is constructed with: crossing from the
    /// <c>lineStart</c> side to the <c>lineEnd</c> side is "forward".
    ///
    /// The first forward crossing ever seen only starts the clock rather than
    /// completing a lap - a rover's position when the station starts
    /// listening is arbitrary, so there is no meaningful prior lap to report
    /// yet. Every forward crossing after that completes one.
    ///
    /// A fix that lands exactly on the line does not resolve to either side
    /// by itself - see <see cref="_lastDefinitePosition"/> for why it is
    /// held pending rather than treated as a non-crossing.
    /// </summary>
    public class LapTimer
    {
        private readonly TrackPoint _lineStart;
        private readonly TrackPoint _lineEnd;

        /// <summary>
        /// The most recent fix that fell unambiguously on one side of the
        /// line or the other - never a fix that landed exactly on it. A fix
        /// can land exactly on the line for real: it's derived from the same
        /// linear projection as the rover's own quantized lat/lon (telemetry
        /// rounds to 1e-7 degrees), so an exact match isn't the coincidence
        /// it would be with unrelated, unquantized coordinates. Advancing
        /// this field past such a fix would make the *next* fix's segment
        /// start from an ambiguous point too, and a segment that starts on
        /// the line can never satisfy a strict side check no matter which
        /// side it ends on - silently losing a real crossing that happened
        /// to sample exactly on the line. Instead, an on-line fix is treated
        /// as pending: this field holds still, so the segment being tested
        /// for a crossing implicitly spans across it to whichever side the
        /// next unambiguous fix resolves to.
        /// </summary>
        private TrackPoint? _lastDefinitePosition;

        private bool _timerRunning;
        private DateTime _lapStartUtc;

        public LapTimer(TrackPoint lineStart, TrackPoint lineEnd)
        {
            _lineStart = lineStart;
            _lineEnd = lineEnd;
        }

        public int LapCount { get; private set; }

        public TimeSpan? LastLapTime { get; private set; }

        /// <summary>
        /// Feeds this rover's latest fix in. Returns true when this update
        /// completed a lap - the first forward crossing does not count as
        /// one, so this can return false even on a genuine, correctly
        /// detected crossing.
        /// </summary>
        public bool Update(TrackPoint position, DateTime timestampUtc)
        {
            bool completedLap = false;
            double side = Cross(_lineStart, _lineEnd, position);

            if (_lastDefinitePosition.HasValue && side != 0 &&
                CrossesForward(_lastDefinitePosition.Value, position))
            {
                if (_timerRunning)
                {
                    LastLapTime = timestampUtc - _lapStartUtc;
                    LapCount++;
                    completedLap = true;
                }

                _timerRunning = true;
                _lapStartUtc = timestampUtc;
            }

            if (side != 0)
            {
                _lastDefinitePosition = position;
            }

            return completedLap;
        }

        /// <summary>
        /// True when the segment from <paramref name="from"/> to
        /// <paramref name="to"/> properly intersects the start line, in the
        /// direction from <see cref="_lineStart"/>'s side to
        /// <see cref="_lineEnd"/>'s side.
        ///
        /// Uses the standard four-orientation segment intersection test: the
        /// two segments intersect only if each one's endpoints fall on
        /// opposite sides of the other. That rules out a segment that merely
        /// crosses the *infinite* line through the start line without
        /// reaching the finite stretch of track it actually spans.
        /// </summary>
        private bool CrossesForward(TrackPoint from, TrackPoint to)
        {
            double lineSide1 = Cross(_lineStart, _lineEnd, from);
            double lineSide2 = Cross(_lineStart, _lineEnd, to);
            if (!(lineSide1 < 0 && lineSide2 > 0)) return false;

            double moveSide1 = Cross(from, to, _lineStart);
            double moveSide2 = Cross(from, to, _lineEnd);
            return (moveSide1 < 0 && moveSide2 > 0) || (moveSide1 > 0 && moveSide2 < 0);
        }

        /// <summary>
        /// Signed area of the triangle (origin, a, b) using longitude as the
        /// x axis and latitude as the y axis - positive when b is to the left
        /// of the ray from origin through a, negative when it is to the
        /// right.
        /// </summary>
        private static double Cross(TrackPoint origin, TrackPoint a, TrackPoint b)
        {
            double ax = a.Longitude - origin.Longitude;
            double ay = a.Latitude - origin.Latitude;
            double bx = b.Longitude - origin.Longitude;
            double by = b.Latitude - origin.Latitude;

            return ax * by - ay * bx;
        }
    }
}
