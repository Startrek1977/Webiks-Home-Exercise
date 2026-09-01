using System;

namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// Wire codec for the RoverLink RL-100 link.
    ///
    /// Telemetry frames (rover to station) are 37 bytes and begin with the
    /// ASCII marker "RL". Command frames (station to rover) are 10 bytes and
    /// begin with "RC". Both are protected by a trailing CRC-8.
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
    /// </summary>
    /// <remarks>
    /// The multi-byte fields are little endian and are read and written with
    /// <see cref="BitConverter"/>, which is correct on every runtime this
    /// station targets.
    /// </remarks>
    public static class FrameCodec
    {
        public const int TelemetryFrameLength = 37;
        public const int CommandFrameLength = 10;
        public const byte ProtocolVersion = 0x01;

        private const byte MarkerRover = 0x52;      // 'R'
        private const byte MarkerTelemetry = 0x4C;  // 'L'
        private const byte MarkerCommand = 0x43;    // 'C'

        private const short ControlInputLimit = 1000;

        private const byte EmergencyStopFlag = 0x01;
        private const byte ArmFlag = 0x02;

        /// <summary>
        /// Decodes a telemetry frame. Returns false when the buffer is not a
        /// well formed frame for a supported protocol version, or when the
        /// checksum does not match.
        /// </summary>
        /// <remarks>
        /// This sits directly on the UDP receive path, so a malformed
        /// datagram is reported as a false return rather than an exception.
        /// </remarks>
        public static bool TryDecode(byte[] buffer, int length, out TelemetryFrame frame)
        {
            frame = null;

            if (buffer == null) return false;
            if (length != TelemetryFrameLength) return false;
            if (buffer.Length < length) return false;

            if (buffer[0] != MarkerRover || buffer[1] != MarkerTelemetry) return false;
            if (buffer[2] != ProtocolVersion) return false;
            if (buffer[TelemetryFrameLength - 1] != Crc8.Compute(buffer, 0, TelemetryFrameLength - 1)) return false;

            frame = new TelemetryFrame(
                roverId: buffer[3],
                sequence: BitConverter.ToUInt32(buffer, 4),
                timestampMs: BitConverter.ToUInt64(buffer, 8),
                latitudeE7: BitConverter.ToInt32(buffer, 16),
                longitudeE7: BitConverter.ToInt32(buffer, 20),
                headingDeci: BitConverter.ToUInt16(buffer, 24),
                speedCmS: BitConverter.ToUInt16(buffer, 26),
                batteryMilliVolts: BitConverter.ToUInt16(buffer, 28),
                signalPercent: buffer[30],
                motorTempDeciC: BitConverter.ToInt16(buffer, 31),
                tiltDeciDeg: BitConverter.ToInt16(buffer, 33),
                statusFlags: buffer[35]);

            return true;
        }

        /// <summary>
        /// Builds a drive command frame. Throttle and steering are clamped to
        /// the controller range of -1000 to 1000.
        /// </summary>
        public static byte[] EncodeCommand(byte roverId, short throttle, short steering, bool emergencyStop, bool armed)
        {
            byte[] frame = new byte[CommandFrameLength];

            frame[0] = MarkerRover;
            frame[1] = MarkerCommand;
            frame[2] = ProtocolVersion;
            frame[3] = roverId;

            BitConverter.GetBytes(Clamp(throttle)).CopyTo(frame, 4);
            BitConverter.GetBytes(Clamp(steering)).CopyTo(frame, 6);

            byte flags = 0x00;
            if (emergencyStop) flags |= EmergencyStopFlag;
            if (armed) flags |= ArmFlag;
            frame[8] = flags;

            frame[CommandFrameLength - 1] = Crc8.Compute(frame, 0, CommandFrameLength - 1);

            return frame;
        }

        private static short Clamp(short value)
        {
            if (value > ControlInputLimit) return ControlInputLimit;
            if (value < -ControlInputLimit) return -ControlInputLimit;

            return value;
        }
    }
}
