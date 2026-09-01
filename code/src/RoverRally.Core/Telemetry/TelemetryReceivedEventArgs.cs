using System;

namespace RoverRally.Core.Telemetry
{
    public class TelemetryReceivedEventArgs : EventArgs
    {
        public TelemetryReceivedEventArgs(TelemetryFrame frame)
        {
            Frame = frame;
        }

        public TelemetryFrame Frame { get; private set; }
    }
}
