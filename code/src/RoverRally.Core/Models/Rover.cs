using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        private DateTime _lastFrameUtc;
        private uint _lastSequence;

        public byte Id { get; set; }
        public string Name { get; set; }
        public string ChassisType { get; set; }
        public string RadioSerial { get; set; }

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
