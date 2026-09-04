using System;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// Sends drive commands to the rovers. The RL-100 base station relays
    /// whatever arrives on the command port to the addressed rover.
    /// </summary>
    public class CommandSender : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly string _host;
        private readonly int _port;
        private readonly ILogger _logger;

        /// <summary>
        /// <paramref name="logger"/> defaults to the static facade's own
        /// logger (#22) so a caller that doesn't care still gets console and
        /// rolling-file output; MainWindow passes a real one explicitly.
        /// </summary>
        public CommandSender(string host, int port, ILogger logger = null)
        {
            _host = host;
            _port = port;
            _udp = new UdpClient();
            _logger = logger ?? Log.CreateLogger<CommandSender>();
        }

        public void Send(byte roverId, short throttle, short steering, bool emergencyStop, bool armed)
        {
            try
            {
                byte[] frame = FrameCodec.EncodeCommand(roverId, throttle, steering, emergencyStop, armed);
                _udp.Send(frame, frame.Length, _host, _port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send a command to rover " + roverId);
            }
        }

        public void Dispose()
        {
            try { _udp.Close(); }
            catch (Exception ex) { _logger.LogDebug("Closing command socket: " + ex.Message); }
        }
    }
}
