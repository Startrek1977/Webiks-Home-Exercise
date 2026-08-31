using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RoverLink.Telemetry;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// Listens for RL-100 telemetry on the station's UDP port and raises one
    /// event per decoded frame. Events are raised on the listener thread.
    /// </summary>
    public class TelemetryClient : IDisposable
    {
        private UdpClient _udp;
        private Thread _worker;
        private volatile bool _running;
        private volatile bool _reconnecting;
        private int _port;

        public event EventHandler<TelemetryReceivedEventArgs> FrameReceived;
        public event EventHandler ConnectionStateChanged;

        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>
        /// True while the listener is rebinding after a socket fault. Frames
        /// are not delivered during this window.
        /// </summary>
        public bool IsReconnecting
        {
            get { return _reconnecting; }
        }

        public void Start(int port)
        {
            if (_running) return;

            _port = port;
            _running = true;

            _worker = new Thread(Listen);
            _worker.IsBackground = true;
            _worker.Name = "RoverLink telemetry";
            _worker.Start();

            Log.Info("Telemetry listener started on UDP " + port + ".");
        }

        public void Stop()
        {
            _running = false;

            if (_udp != null)
            {
                try { _udp.Close(); }
                catch (Exception ex) { Log.Debug("Closing telemetry socket: " + ex.Message); }
                _udp = null;
            }

            Log.Info("Telemetry listener stopped.");
        }

        private void Listen()
        {
            while (_running)
            {
                try
                {
                    if (_udp == null)
                    {
                        _udp = new UdpClient(_port);
                        SetReconnecting(false);
                    }

                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] datagram = _udp.Receive(ref sender);

                    TelemetryFrame frame;
                    if (!FrameCodec.TryDecode(datagram, datagram.Length, out frame))
                    {
                        Log.Debug("Discarded a malformed frame from " + sender + ".");
                        continue;
                    }

                    EventHandler<TelemetryReceivedEventArgs> handler = FrameReceived;
                    if (handler != null) handler(this, new TelemetryReceivedEventArgs(frame));
                }
                catch (SocketException ex)
                {
                    if (!_running) break;

                    Log.Warn("Telemetry socket fault, rebinding: " + ex.Message);
                    SetReconnecting(true);

                    if (_udp != null)
                    {
                        try { _udp.Close(); }
                        catch (Exception close) { Log.Debug("Closing faulted socket: " + close.Message); }
                        _udp = null;
                    }

                    Thread.Sleep(2000);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void SetReconnecting(bool value)
        {
            if (_reconnecting == value) return;

            _reconnecting = value;

            EventHandler handler = ConnectionStateChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
