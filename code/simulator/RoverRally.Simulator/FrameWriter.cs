namespace RoverRally.Simulator;

/// <summary>
/// The wire format the RL-100 radio puts on the air. This is the rover side of
/// the link: it writes telemetry frames and reads drive commands.
///
/// Telemetry frame, 37 bytes, little endian:
///   0   2   marker 'R','L'
///   2   1   protocol version (1)
///   3   1   rover id
///   4   4   sequence number, uint32
///   8   8   timestamp, uint64, milliseconds since the unix epoch
///   16  4   latitude,  int32, degrees * 1e7
///   20  4   longitude, int32, degrees * 1e7
///   24  2   heading,   uint16, degrees * 10
///   26  2   speed,     uint16, centimetres per second
///   28  2   battery,   uint16, millivolts
///   30  1   signal quality, 0 - 100
///   31  2   motor temperature, int16, degrees C * 10
///   33  2   tilt, int16, degrees * 10
///   35  1   status flags: bit0 armed, bit1 emergency stop, bit2 charging, bit3 gps fix
///   36  1   CRC-8 over bytes 0 - 35
///
/// Command frame, 10 bytes, little endian:
///   0   2   marker 'R','C'
///   2   1   protocol version (1)
///   3   1   rover id
///   4   2   throttle, int16, -1000 to 1000
///   6   2   steering, int16, -1000 to 1000
///   8   1   flags: bit0 emergency stop, bit1 arm
///   9   1   CRC-8 over bytes 0 - 8
///
/// The CRC is CRC-8 with polynomial 0x07 and an initial value of 0x00.
/// </summary>
public static class FrameWriter
{
    public const int TelemetryFrameLength = 37;
    public const int CommandFrameLength = 10;

    private const byte ProtocolVersion = 0x01;

    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0x00;

        foreach (byte value in data)
        {
            crc ^= value;

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
        frame[36] = Crc8(frame.AsSpan(0, TelemetryFrameLength - 1));

        return frame;
    }

    public static bool TryReadCommand(byte[] datagram, out DriveCommand command)
    {
        command = default;

        if (datagram.Length != CommandFrameLength) return false;
        if (datagram[0] != 0x52 || datagram[1] != 0x43) return false;
        if (datagram[2] != ProtocolVersion) return false;
        if (datagram[9] != Crc8(datagram.AsSpan(0, CommandFrameLength - 1))) return false;

        byte flags = datagram[8];

        command = new DriveCommand(
            RoverId: datagram[3],
            Throttle: BitConverter.ToInt16(datagram, 4),
            Steering: BitConverter.ToInt16(datagram, 6),
            EmergencyStop: (flags & 0x01) != 0,
            Armed: (flags & 0x02) != 0);

        return true;
    }
}

public readonly record struct DriveCommand(byte RoverId, short Throttle, short Steering, bool EmergencyStop, bool Armed);

public struct TelemetryReading
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
