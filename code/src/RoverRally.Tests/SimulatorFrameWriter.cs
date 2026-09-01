using System;

namespace RoverRally.Tests
{
    /// <summary>
    /// A transcription of code\simulator\RoverRally.Simulator\FrameWriter.cs,
    /// which is the authoritative description of the RL-100 wire format. The
    /// simulator targets net8.0 and is not part of this solution, so the tests
    /// cannot reference it; this mirror stands in for it. It deliberately
    /// shares no code with the implementation under test.
    ///
    /// Keep it in step with FrameWriter.cs if the simulator ever changes.
    /// </summary>
    internal static class SimulatorFrameWriter
    {
        public const int TelemetryFrameLength = 37;
        public const int CommandFrameLength = 10;

        private const byte ProtocolVersion = 0x01;

        public static byte Crc8(byte[] data, int offset, int count)
        {
            byte crc = 0x00;

            for (int i = 0; i < count; i++)
            {
                crc ^= data[offset + i];

                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
                }
            }

            return crc;
        }

        public static byte[] WriteTelemetry(TelemetryReading reading)
        {
            byte[] frame = new byte[TelemetryFrameLength];

            frame[0] = 0x52;
            frame[1] = 0x4C;
            frame[2] = ProtocolVersion;
            frame[3] = reading.RoverId;

            BitConverter.GetBytes(reading.Sequence).CopyTo(frame, 4);
            BitConverter.GetBytes(reading.TimestampMs).CopyTo(frame, 8);
            BitConverter.GetBytes(reading.LatitudeE7).CopyTo(frame, 16);
            BitConverter.GetBytes(reading.LongitudeE7).CopyTo(frame, 20);
            BitConverter.GetBytes(reading.HeadingDeci).CopyTo(frame, 24);
            BitConverter.GetBytes(reading.SpeedCmS).CopyTo(frame, 26);
            BitConverter.GetBytes(reading.BatteryMilliVolts).CopyTo(frame, 28);
            frame[30] = reading.SignalPercent;
            BitConverter.GetBytes(reading.MotorTempDeciC).CopyTo(frame, 31);
            BitConverter.GetBytes(reading.TiltDeciDeg).CopyTo(frame, 33);
            frame[35] = reading.StatusFlags;
            frame[36] = Crc8(frame, 0, TelemetryFrameLength - 1);

            return frame;
        }

        public static bool TryReadCommand(byte[] datagram, out DriveCommand command)
        {
            command = default(DriveCommand);

            if (datagram.Length != CommandFrameLength) return false;
            if (datagram[0] != 0x52 || datagram[1] != 0x43) return false;
            if (datagram[2] != ProtocolVersion) return false;
            if (datagram[9] != Crc8(datagram, 0, CommandFrameLength - 1)) return false;

            command = new DriveCommand
            {
                RoverId = datagram[3],
                Throttle = BitConverter.ToInt16(datagram, 4),
                Steering = BitConverter.ToInt16(datagram, 6),
                EmergencyStop = (datagram[8] & 0x01) != 0,
                Armed = (datagram[8] & 0x02) != 0
            };

            return true;
        }
    }

    internal struct DriveCommand
    {
        public byte RoverId;
        public short Throttle;
        public short Steering;
        public bool EmergencyStop;
        public bool Armed;
    }

    internal struct TelemetryReading
    {
        public byte RoverId;
        public uint Sequence;
        public ulong TimestampMs;
        public int LatitudeE7;
        public int LongitudeE7;
        public ushort HeadingDeci;
        public ushort SpeedCmS;
        public ushort BatteryMilliVolts;
        public byte SignalPercent;
        public short MotorTempDeciC;
        public short TiltDeciDeg;
        public byte StatusFlags;
    }
}
