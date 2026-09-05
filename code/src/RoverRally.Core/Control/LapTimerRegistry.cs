using System.Collections.Generic;
using RoverRally.Core.Models;

namespace RoverRally.Core.Control
{
    /// <summary>
    /// Keys one <see cref="LapTimer"/> per rover, so each vehicle's lap count
    /// and last lap time are independent of the others and survive the
    /// station's selection moving away and back - the same reasoning as
    /// <see cref="DriveControllerRegistry"/> (#35).
    /// </summary>
    public class LapTimerRegistry
    {
        private readonly TrackPoint _lineStart;
        private readonly TrackPoint _lineEnd;
        private readonly Dictionary<byte, LapTimer> _timers = new Dictionary<byte, LapTimer>();

        public LapTimerRegistry(TrackPoint lineStart, TrackPoint lineEnd)
        {
            _lineStart = lineStart;
            _lineEnd = lineEnd;
        }

        /// <summary>
        /// The lap timer for this rover, creating one on first use. The same
        /// instance is returned for the same id every time.
        /// </summary>
        public LapTimer For(byte roverId)
        {
            LapTimer? timer;
            if (!_timers.TryGetValue(roverId, out timer))
            {
                timer = new LapTimer(_lineStart, _lineEnd);
                _timers[roverId] = timer;
            }

            return timer;
        }
    }
}
