namespace RoverRally.Simulator;

/// <summary>
/// One simulated vehicle running laps of the proving ground. Positions are
/// generated in the same canvas space the site survey uses and converted to
/// degrees on the way out, so the rovers always land on the drawn track.
/// </summary>
public sealed class RoverSim
{
    // Site survey extent. These match the TrackNorth/South/West/East settings
    // the station ships with.
    private const double TrackNorth = 32.2830;
    private const double TrackSouth = 32.2770;
    private const double TrackWest = 34.9160;
    private const double TrackEast = 34.9250;

    private const double CanvasWidth = 720;
    private const double CanvasHeight = 480;

    // The surveyed loop is about 850 m across, which works out at roughly this
    // many metres per canvas unit. Adjust if the survey is redone.
    private const double MetresPerCanvasUnit = 1.176;

    private const double StraightLength = 420;
    private const double ArcRadius = 90;
    private static readonly double ArcLength = Math.PI * ArcRadius;
    private static readonly double LoopLength = 2 * StraightLength + 2 * ArcLength;

    // Full steering lock moves the vehicle this far off the racing line. The
    // asphalt is about 34 canvas units wide, so this keeps it on the surface.
    private const double MaxLateralOffset = 14;

    private const double MaxCommandedSpeedCmS = 700;

    private readonly Random _random;
    private readonly double _cruiseSpeedCmS;
    private readonly bool _takesWideLines;

    private double _distanceAlongLoop;
    private double _speedCmS;
    private double _lateralOffset;
    private double _batteryMilliVolts = 12480;
    private double _motorTempC = 24;
    private double _excursionTimer;
    private double _sinceCommand = 999;

    private short _commandedThrottle;
    private short _commandedSteering;
    private bool _armed;
    private bool _emergencyStopHeld;

    public RoverSim(byte id, string name, double cruiseSpeedCmS, double startOffset, bool takesWideLines, int seed)
    {
        Id = id;
        Name = name;
        _cruiseSpeedCmS = cruiseSpeedCmS;
        _distanceAlongLoop = startOffset;
        _takesWideLines = takesWideLines;
        _random = new Random(seed);
        _speedCmS = cruiseSpeedCmS;
    }

    public byte Id { get; }
    public string Name { get; }
    public double SpeedCmS => Math.Abs(_speedCmS);
    public double BatteryMilliVolts => _batteryMilliVolts;
    public bool IsOutsideFence { get; private set; }
    public bool IsEmergencyStopped => _emergencyStopHeld;

    /// <summary>
    /// Acts on a command frame. The vehicle obeys the frame it has just been
    /// handed and keeps no memory of the previous one, which is what the
    /// integration notes describe and what the station has to work with.
    ///
    /// Returns true when this frame is the one that engaged the stop, so the
    /// console reports it once instead of on every frame that holds it.
    /// </summary>
    public bool Apply(DriveCommand command)
    {
        bool stopNewlyEngaged = command.EmergencyStop && !_emergencyStopHeld;

        _commandedThrottle = command.Throttle;
        _commandedSteering = command.Steering;
        _armed = command.Armed;
        _emergencyStopHeld = command.EmergencyStop;
        _sinceCommand = 0;

        return stopNewlyEngaged;
    }

    public void Advance(double seconds)
    {
        _sinceCommand += seconds;

        bool underCommand = _armed && _sinceCommand < 2.0;

        if (_emergencyStopHeld)
        {
            // A commanded stop is the brakes going on, not a coast down.
            _speedCmS = 0;
            _lateralOffset = 0;
        }
        else
        {
            double target = underCommand
                ? _commandedThrottle / 1000.0 * MaxCommandedSpeedCmS
                : _cruiseSpeedCmS + Math.Sin(_distanceAlongLoop / 90.0) * 40.0;

            // Ease toward the target so the readouts move the way a vehicle does.
            _speedCmS += (target - _speedCmS) * Math.Min(1.0, seconds * 2.5);
            if (Math.Abs(_speedCmS) < 0.5) _speedCmS = 0;

            double steerTarget = underCommand ? _commandedSteering / 1000.0 * MaxLateralOffset : 0;
            _lateralOffset += (steerTarget - _lateralOffset) * Math.Min(1.0, seconds * 2.0);
        }

        double canvasUnitsPerSecond = _speedCmS / 100.0 / MetresPerCanvasUnit;
        double advanced = _distanceAlongLoop + canvasUnitsPerSecond * seconds;
        _distanceAlongLoop = ((advanced % LoopLength) + LoopLength) % LoopLength;

        _batteryMilliVolts -= (12 + Math.Abs(_speedCmS) * 0.06) * seconds;
        if (_batteryMilliVolts < 9200) _batteryMilliVolts = 12480;

        double targetTemp = 24 + Math.Abs(_speedCmS) * 0.055;
        _motorTempC += (targetTemp - _motorTempC) * Math.Min(1.0, seconds * 0.2);

        if (_takesWideLines)
        {
            _excursionTimer += seconds;
            if (_excursionTimer > 45) _excursionTimer = -6;
        }
    }

    /// <summary>
    /// Builds the frame the radio would put on the air this tick. The glitch
    /// flags stand in for the two faults the hardware actually shows: the
    /// receiver losing its fix, and the pack voltage sense failing to read.
    /// </summary>
    public TelemetryReading Read(uint sequence, bool noFix, bool batterySenseFailed)
    {
        (double x, double y) = PointAt(_distanceAlongLoop);
        double heading = HeadingAt(_distanceAlongLoop);

        // Steering moves the vehicle off the racing line, to the right of the
        // direction of travel for positive lock.
        double headingRad = heading * Math.PI / 180.0;
        x += Math.Cos(headingRad) * _lateralOffset;
        y += Math.Sin(headingRad) * _lateralOffset;

        bool runningWide = _takesWideLines && _excursionTimer < 0 && _distanceAlongLoop < StraightLength;
        if (runningWide) y -= 105;

        IsOutsideFence = runningWide;

        double latitude = TrackNorth - y / CanvasHeight * (TrackNorth - TrackSouth);
        double longitude = TrackWest + x / CanvasWidth * (TrackEast - TrackWest);

        byte flags = 0;
        if (_armed) flags |= 0x01;
        if (_emergencyStopHeld) flags |= 0x02;
        if (!noFix) flags |= 0x08;

        return new TelemetryReading
        {
            RoverId = Id,
            Sequence = sequence,
            TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LatitudeE7 = noFix ? 0 : (int)Math.Round(latitude * 1e7),
            LongitudeE7 = noFix ? 0 : (int)Math.Round(longitude * 1e7),
            HeadingDeci = (ushort)(((int)Math.Round(heading * 10) % 3600 + 3600) % 3600),
            SpeedCmS = (ushort)Math.Round(Math.Abs(_speedCmS)),
            BatteryMilliVolts = batterySenseFailed ? (ushort)0 : (ushort)Math.Round(_batteryMilliVolts),
            SignalPercent = (byte)(72 + _random.Next(0, 28)),
            MotorTempDeciC = (short)Math.Round(_motorTempC * 10),
            TiltDeciDeg = (short)Math.Round(Math.Sin(_distanceAlongLoop / 40.0) * 55),
            StatusFlags = flags
        };
    }

    private static (double X, double Y) PointAt(double distance)
    {
        if (distance < StraightLength)
        {
            return (150 + distance, 120);
        }

        if (distance < StraightLength + ArcLength)
        {
            double sweep = (distance - StraightLength) / ArcLength * Math.PI;
            return (570 + ArcRadius * Math.Sin(sweep), 210 - ArcRadius * Math.Cos(sweep));
        }

        if (distance < 2 * StraightLength + ArcLength)
        {
            return (570 - (distance - StraightLength - ArcLength), 300);
        }

        double back = (distance - 2 * StraightLength - ArcLength) / ArcLength * Math.PI;
        return (150 - ArcRadius * Math.Sin(back), 210 + ArcRadius * Math.Cos(back));
    }

    private static double HeadingAt(double distance)
    {
        const double step = 0.5;

        (double x1, double y1) = PointAt(distance);
        (double x2, double y2) = PointAt((distance + step) % LoopLength);

        double degrees = Math.Atan2(x2 - x1, -(y2 - y1)) * 180.0 / Math.PI;
        return (degrees + 360) % 360;
    }
}
