using System;

namespace RoverRally.Core.Telemetry
{
    public class TelemetryReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Stamped here, on the listener thread, at the moment the frame was
        /// decoded - not later, whenever a UI-thread handler gets around to
        /// applying it. <see cref="TelemetryClient.FrameReceived"/> is raised
        /// on the listener thread and consumers dispatch it onward
        /// (<see cref="System.Windows.Threading.Dispatcher.BeginInvoke"/>
        /// merely queues that); re-stamping "now" once the queued handler
        /// finally runs would record how long the UI thread took to get to
        /// it, not when the vehicle actually reported.
        /// </summary>
        public TelemetryReceivedEventArgs(TelemetryFrame frame)
        {
            Frame = frame;
            ReceivedUtc = DateTime.UtcNow;
        }

        public TelemetryFrame Frame { get; private set; }
        public DateTime ReceivedUtc { get; private set; }
    }
}
