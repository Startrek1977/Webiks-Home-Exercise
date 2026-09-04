using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// Listens for RL-100 telemetry on the station's UDP port and raises one
    /// event per decoded frame. Events are raised on the listener thread.
    /// </summary>
    public class TelemetryClient : IDisposable
    {
        private readonly ILogger _logger;

        private UdpClient _udp;
        private Thread _worker;
        private volatile bool _running;
        private volatile bool _reconnecting;
        private int _port;

        /// <summary>
        /// <paramref name="logger"/> defaults to the static facade's own
        /// logger (#22) so a caller that doesn't care still gets console and
        /// rolling-file output; MainWindow passes a real one explicitly.
        /// </summary>
        public TelemetryClient(ILogger logger = null)
        {
            _logger = logger ?? Log.CreateLogger<TelemetryClient>();
        }

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

            _logger.LogInformation("Telemetry listener started on UDP " + port + ".");
        }

        public void Stop()
        {
            _running = false;

            if (_udp != null)
            {
                try { _udp.Close(); }
                catch (Exception ex) { _logger.LogDebug("Closing telemetry socket: " + ex.Message); }
                _udp = null;
            }

            _logger.LogInformation("Telemetry listener stopped.");
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
                        _logger.LogDebug("Discarded a malformed frame from " + sender + ".");
                        continue;
                    }

                    EventHandler<TelemetryReceivedEventArgs> handler = FrameReceived;
                    if (handler != null) handler(this, new TelemetryReceivedEventArgs(frame));
                }
                catch (SocketException ex)
                {
                    if (!_running) break;

                    _logger.LogWarning("Telemetry socket fault, rebinding: " + ex.Message);
                    SetReconnecting(true);

                    if (_udp != null)
                    {
                        try { _udp.Close(); }
                        catch (Exception close) { _logger.LogDebug("Closing faulted socket: " + close.Message); }
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
