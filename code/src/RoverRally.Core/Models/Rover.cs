using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RoverRally.Core.Telemetry;

namespace RoverRally.Core.Models
{
    /// <summary>
    /// Live state for a single rover on the proving ground. Instances are
    /// created from the roster file and then updated in place as telemetry
    /// arrives, so the grid and the map always share one object per rover.
    /// </summary>
    public class Rover : INotifyPropertyChanged
    {
        private RoverStatus _status;
        private TrackPoint _position;
        private double _heading;
        private int _speedCmS;
        private int _batteryMilliVolts;
        private int _signalPercent;
        private double _motorTempC;
        private double _tiltDegrees;
        private bool _isArmed;
        private bool _isEmergencyStopped;
        private DateTime _lastFrameUtc;
        private uint _lastSequence;

        public byte Id { get; set; }
        public string? Name { get; set; }
        public string? ChassisType { get; set; }
        public string? RadioSerial { get; set; }

        public RoverStatus Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(); }
        }

        public TrackPoint Position
        {
            get { return _position; }
            set { _position = value; OnPropertyChanged(); }
        }

        public double Heading
        {
            get { return _heading; }
            set { _heading = value; OnPropertyChanged(); }
        }

        public int SpeedCmS
        {
            get { return _speedCmS; }
            set { _speedCmS = value; OnPropertyChanged(); }
        }

        public int BatteryMilliVolts
        {
            get { return _batteryMilliVolts; }
            set { _batteryMilliVolts = value; OnPropertyChanged(); OnPropertyChanged("BatteryPercent"); }
        }

        public int BatteryPercent
        {
            get { return Units.BatteryGauge.ToPercent(_batteryMilliVolts); }
        }

        public int SignalPercent
        {
            get { return _signalPercent; }
            set { _signalPercent = value; OnPropertyChanged(); }
        }

        public double MotorTemperatureC
        {
            get { return _motorTempC; }
            set { _motorTempC = value; OnPropertyChanged(); }
        }

        public double TiltDegrees
        {
            get { return _tiltDegrees; }
            set { _tiltDegrees = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Armed state as the vehicle itself reports it, from bit 0 of the
        /// telemetry status byte. This is what the rover believes, not what
        /// the station last asked for; the two disagreeing is worth seeing.
        /// </summary>
        public bool IsArmed
        {
            get { return _isArmed; }
            set { _isArmed = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Emergency stop as the vehicle itself reports it, from bit 1 of the
        /// telemetry status byte. Never write the station's latch back from
        /// this - the vehicle is not the authority on whether an operator
        /// re-armed.
        /// </summary>
        public bool IsEmergencyStopped
        {
            get { return _isEmergencyStopped; }
            set { _isEmergencyStopped = value; OnPropertyChanged(); }
        }

        public DateTime LastFrameUtc
        {
            get { return _lastFrameUtc; }
            set { _lastFrameUtc = value; OnPropertyChanged(); }
        }

        public uint LastSequence
        {
            get { return _lastSequence; }
            set { _lastSequence = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Applies a decoded telemetry frame to this rover's live state.
        /// <see cref="TelemetryFrame.HasGpsFix"/> gates <see cref="Position"/>
        /// only - when the receiver has no fix the protocol zeroes the
        /// latitude and longitude fields, so that reading is not a position
        /// and the last known one is kept. Every other field is still valid
        /// on a no-fix frame and is applied unconditionally.
        /// </summary>
        public void ApplyFrame(TelemetryFrame frame, DateTime receivedUtc)
        {
            if (frame.HasGpsFix)
            {
                Position = new TrackPoint(frame.LatitudeE7 / 1e7, frame.LongitudeE7 / 1e7);
            }

            Heading = frame.HeadingDeci / 10.0;
            SpeedCmS = frame.SpeedCmS;
            BatteryMilliVolts = frame.BatteryMilliVolts;
            SignalPercent = frame.SignalPercent;
            MotorTemperatureC = frame.MotorTempDeciC / 10.0;
            TiltDegrees = frame.TiltDeciDeg / 10.0;
            LastFrameUtc = receivedUtc;
            LastSequence = frame.Sequence;
            IsArmed = frame.IsArmed;
            IsEmergencyStopped = frame.IsEmergencyStopped;

            if (frame.IsEmergencyStopped) Status = RoverStatus.Stopped;
            else if (frame.IsCharging) Status = RoverStatus.Charging;
            else if (frame.SpeedCmS > 0) Status = RoverStatus.Driving;
            else Status = RoverStatus.Idle;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
