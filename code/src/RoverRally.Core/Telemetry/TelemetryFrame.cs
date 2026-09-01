namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// A single decoded telemetry report from an RL-100 equipped rover.
    /// All values are carried in the units the flight controller uses;
    /// scaling to display units is the responsibility of the host application.
    /// Instances are produced by <see cref="FrameCodec.TryDecode"/> and are
    /// immutable once decoded.
    /// </summary>
    public class TelemetryFrame
    {
        private const byte ArmedFlag = 0x01;
        private const byte EmergencyStopFlag = 0x02;
        private const byte ChargingFlag = 0x04;
        private const byte GpsFixFlag = 0x08;

        public TelemetryFrame(byte roverId, uint sequence, ulong timestampMs,
                              int latitudeE7, int longitudeE7,
                              ushort headingDeci, ushort speedCmS, ushort batteryMilliVolts,
                              byte signalPercent, short motorTempDeciC, short tiltDeciDeg,
                              byte statusFlags)
        {
            RoverId = roverId;
            Sequence = sequence;
            TimestampMs = timestampMs;
            LatitudeE7 = latitudeE7;
            LongitudeE7 = longitudeE7;
            HeadingDeci = headingDeci;
            SpeedCmS = speedCmS;
            BatteryMilliVolts = batteryMilliVolts;
            SignalPercent = signalPercent;
            MotorTempDeciC = motorTempDeciC;
            TiltDeciDeg = tiltDeciDeg;
            StatusFlags = statusFlags;
        }

        /// <summary>Identifier of the reporting vehicle.</summary>
        public byte RoverId { get; private set; }

        /// <summary>Transmission sequence number. Per transmission, not per vehicle, and it wraps.</summary>
        public uint Sequence { get; private set; }

        /// <summary>Milliseconds since the unix epoch, UTC.</summary>
        public ulong TimestampMs { get; private set; }

        /// <summary>Latitude in degrees multiplied by 1e7.</summary>
        public int LatitudeE7 { get; private set; }

        /// <summary>Longitude in degrees multiplied by 1e7.</summary>
        public int LongitudeE7 { get; private set; }

        /// <summary>Heading in degrees multiplied by 10 (0 - 3599).</summary>
        public ushort HeadingDeci { get; private set; }

        /// <summary>Ground speed in centimetres per second.</summary>
        public ushort SpeedCmS { get; private set; }

        /// <summary>Pack voltage in millivolts.</summary>
        public ushort BatteryMilliVolts { get; private set; }

        /// <summary>Link quality, 0 - 100.</summary>
        public byte SignalPercent { get; private set; }

        /// <summary>Motor temperature in degrees Celsius multiplied by 10.</summary>
        public short MotorTempDeciC { get; private set; }

        /// <summary>Chassis tilt in degrees multiplied by 10.</summary>
        public short TiltDeciDeg { get; private set; }

        /// <summary>Bit 0 armed, bit 1 emergency stop latched, bit 2 charging, bit 3 GPS fix.</summary>
        public byte StatusFlags { get; private set; }

        public bool IsArmed
        {
            get { return (StatusFlags & ArmedFlag) != 0; }
        }

        public bool IsEmergencyStopped
        {
            get { return (StatusFlags & EmergencyStopFlag) != 0; }
        }

        public bool IsCharging
        {
            get { return (StatusFlags & ChargingFlag) != 0; }
        }

        /// <summary>
        /// True when the receiver has a fix. When this is false the latitude
        /// and longitude fields are zeroed and carry no position.
        /// </summary>
        public bool HasGpsFix
        {
            get { return (StatusFlags & GpsFixFlag) != 0; }
        }
    }
}
