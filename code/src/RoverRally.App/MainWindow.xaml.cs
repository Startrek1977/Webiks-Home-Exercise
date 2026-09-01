using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RoverRally.App.ViewModels;
using RoverRally.Core.Configuration;
using RoverRally.Core.Geo;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;
using RoverRally.Core.Roster;
using RoverRally.Core.Session;
using RoverRally.Core.Telemetry;

namespace RoverRally.App
{
    public partial class MainWindow : Window
    {
        private readonly StationViewModel _vm = new StationViewModel();

        private TelemetryClient _telemetry;
        private CommandSender _commands;
        private DispatcherTimer _driveTimer;
        private GeofenceMonitor _geofence;

        private bool _emergencyStopLatched;
        private bool _armed;
        private int _frameCount;
        private readonly HashSet<byte> _fencedAlerted = new HashSet<byte>();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _vm;
            StationNameText.Text = App.StationName;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRoster();
            LoadTrack();
            LoadSessionHistory();
            StartLink();

            _vm.SpeedUnit = StationSettings.PreferredSpeedUnit;

            int lastRover = StationSettings.LastSelectedRoverId;
            if (lastRover > 0)
            {
                _vm.SelectedRover = _vm.Rovers.FirstOrDefault(r => r.Id == lastRover);
            }

            if (_vm.SelectedRover == null && _vm.Rovers.Count > 0)
            {
                _vm.SelectedRover = _vm.Rovers[0];
            }

            Fleet.Bind(_vm);
            Settings.Bind(_vm);
        }

        private void LoadRoster()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StationSettings.RosterPath);

            foreach (Rover rover in RoverRoster.Load(path))
            {
                _vm.Rovers.Add(rover);
            }
        }

        private void LoadTrack()
        {
            Track.Configure(StationSettings.TrackNorth, StationSettings.TrackSouth,
                            StationSettings.TrackWest, StationSettings.TrackEast);

            TrackPoint[] fence = new TrackPoint[]
            {
                new TrackPoint(StationSettings.TrackNorth - 0.0004, StationSettings.TrackWest + 0.0006),
                new TrackPoint(StationSettings.TrackNorth - 0.0004, StationSettings.TrackEast - 0.0006),
                new TrackPoint(StationSettings.TrackSouth + 0.0004, StationSettings.TrackEast - 0.0006),
                new TrackPoint(StationSettings.TrackSouth + 0.0004, StationSettings.TrackWest + 0.0006)
            };

            _geofence = new GeofenceMonitor(fence);
            Track.SetGeofence(fence);
        }

        private void LoadSessionHistory()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StationSettings.SessionCachePath);

            try
            {
                Fleet.SetHistory(SessionCacheFile.Read(path));
            }
            catch (Exception ex)
            {
                Log.Error("Could not load the session cache", ex);
            }
        }

        private void StartLink()
        {
            string host = ConfigurationManager.AppSettings["CommandHost"];
            if (string.IsNullOrEmpty(host)) host = "127.0.0.1";

            _commands = new CommandSender(host, StationSettings.CommandPort);

            _telemetry = new TelemetryClient();
            _telemetry.FrameReceived += Telemetry_FrameReceived;
            _telemetry.ConnectionStateChanged += Telemetry_ConnectionStateChanged;
            _telemetry.Start(StationSettings.TelemetryPort);

            _vm.LinkState = "Listening";

            int interval;
            if (!int.TryParse(ConfigurationManager.AppSettings["DriveCommandIntervalMs"], out interval)) interval = 200;

            _driveTimer = new DispatcherTimer();
            _driveTimer.Interval = TimeSpan.FromMilliseconds(interval);
            _driveTimer.Tick += DriveTimer_Tick;
            _driveTimer.Start();
        }

        private void Telemetry_ConnectionStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _vm.LinkState = _telemetry.IsReconnecting ? "Reconnecting" : "Listening";
            }));
        }

        private void Telemetry_FrameReceived(object sender, TelemetryReceivedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate { ApplyFrame(e.Frame); }));
        }

        private void ApplyFrame(TelemetryFrame frame)
        {
            Rover rover = _vm.Rovers.FirstOrDefault(r => r.Id == frame.RoverId);
            if (rover == null) return;

            rover.Position = new TrackPoint(frame.LatitudeE7 / 1e7, frame.LongitudeE7 / 1e7);
            rover.Heading = frame.HeadingDeci / 10.0;
            rover.SpeedCmS = frame.SpeedCmS;
            rover.BatteryMilliVolts = frame.BatteryMilliVolts;
            rover.SignalPercent = frame.SignalPercent;
            rover.MotorTemperatureC = frame.MotorTempDeciC / 10.0;
            rover.TiltDegrees = frame.TiltDeciDeg / 10.0;
            rover.LastFrameUtc = DateTime.UtcNow;
            rover.LastSequence = frame.Sequence;

            if (frame.IsEmergencyStopped) rover.Status = RoverStatus.Stopped;
            else if (frame.IsCharging) rover.Status = RoverStatus.Charging;
            else if (frame.SpeedCmS > 0) rover.Status = RoverStatus.Driving;
            else rover.Status = RoverStatus.Idle;

            Track.UpdateRover(rover);

            if (_geofence.IsOutside(rover.Position))
            {
                if (_fencedAlerted.Add(rover.Id))
                {
                    Log.Warn(rover.Name + " has left the fenced area.");
                }
            }
            else
            {
                _fencedAlerted.Remove(rover.Id);
            }

            _frameCount++;
            FrameCounterText.Text = _frameCount + " frames";

            if (ReferenceEquals(rover, _vm.SelectedRover))
            {
                _vm.RefreshSelectedReadouts();
            }
        }

        private void DriveTimer_Tick(object sender, EventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null || _commands == null) return;

            short throttle = (short)ThrottleSlider.Value;
            short steering = (short)SteeringSlider.Value;

            _commands.Send(rover.Id, throttle, steering, false, _armed);
        }

        private void Arm_Click(object sender, RoutedEventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null)
            {
                MessageBox.Show("Select a rover first.", "RoverRally", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _armed = !_armed;
            _emergencyStopLatched = false;
            ArmButton.Content = _armed ? "DISARM" : "ARM";

            _commands.Send(rover.Id, 0, 0, false, _armed);
            Log.Info((_armed ? "Armed " : "Disarmed ") + rover.Name + ".");
        }

        private void EmergencyStop_Click(object sender, RoutedEventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null) return;

            if (_telemetry.IsReconnecting)
            {
                MessageBox.Show("The link is reconnecting. Try again in a moment.",
                                "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _emergencyStopLatched = true;
            _commands.Send(rover.Id, 0, 0, true, false);

            Log.Warn("Emergency stop sent to " + rover.Name + ".");
            MessageBox.Show("Emergency stop sent to " + rover.Name + ".",
                            "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_vm.SelectedRover != null)
            {
                StationSettings.LastSelectedRoverId = _vm.SelectedRover.Id;
            }

            StationSettings.PreferredSpeedUnit = _vm.SpeedUnit;

            if (_driveTimer != null) _driveTimer.Stop();
            if (_telemetry != null) _telemetry.Dispose();
            if (_commands != null) _commands.Dispose();
        }
    }
}
