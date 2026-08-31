using System;
using System.Net.Sockets;
using RoverLink.Telemetry;
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

        public CommandSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udp = new UdpClient();
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
                Log.Error("Could not send a command to rover " + roverId, ex);
            }
        }

        public void Dispose()
        {
            try { _udp.Close(); }
            catch (Exception ex) { Log.Debug("Closing command socket: " + ex.Message); }
        }
    }
}
