using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using RoverRally.App.ViewModels;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
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

        private readonly DriveControllerRegistry _driveControllers = new DriveControllerRegistry();

        /// <summary>
        /// UpdateDriveDisplay runs on every telemetry frame for the selected
        /// rover, so a brush built per frame is avoidable allocation on the UI
        /// thread. Frozen so one instance can be shared.
        /// </summary>
        private static readonly Brush QuietStateBrush = CreateQuietStateBrush();

        /// <summary>Resolved once; FindResource walks the tree on every call.</summary>
        private Brush _latchedStateBrush;
        private int _frameCount;
        private readonly HashSet<byte> _fencedAlerted = new HashSet<byte>();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _vm;
            StationNameText.Text = App.StationName;

            _vm.PropertyChanged += ViewModel_PropertyChanged;

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

            UpdateDriveDisplay();
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

        /// <summary>
        /// The drive display names a particular vehicle, so it has to follow
        /// the selection rather than wait for that vehicle's next frame. A rover
        /// that is not transmitting would otherwise leave the previous rover's
        /// state on screen indefinitely, under the new rover's name - which is
        /// precisely the confusion this indicator exists to prevent. As of #35
        /// this also picks up the newly selected rover's own arm/latch state
        /// from its own DriveController, rather than showing a station-wide
        /// flag that may describe a different vehicle.
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedRover") UpdateDriveDisplay();
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
            DateTime receivedUtc = e.ReceivedUtc;
            Dispatcher.BeginInvoke(new Action(delegate { ApplyFrame(e.Frame, receivedUtc); }));
        }

        /// <summary>
        /// <paramref name="receivedUtc"/> is when the listener thread decoded
        /// this frame, not when this method finally runs - BeginInvoke only
        /// queues the call, and the UI thread can be busy with other work
        /// (an ARM click, say) by the time it gets here. Stamping
        /// Rover.LastFrameUtc with "now" at that point would record queueing
        /// delay instead of when the vehicle actually reported, which is
        /// exactly the gap DriveController's re-arm race relies on.
        /// </summary>
        private void ApplyFrame(TelemetryFrame frame, DateTime receivedUtc)
        {
            Rover rover = _vm.Rovers.FirstOrDefault(r => r.Id == frame.RoverId);
            if (rover == null) return;

            rover.ApplyFrame(frame, receivedUtc);

            // A no-fix frame carries no position (the protocol zeroes it) and
            // Rover.ApplyFrame leaves Position at its last known value, so
            // there is nothing new here to draw or to judge the fence
            // against. Calling Track.UpdateRover anyway would just
            // re-append that same retained point to the trail on every
            // no-fix tick, which - given enough of them in a row - evicts
            // real history with copies of a stale point.
            if (frame.HasGpsFix)
            {
                Track.UpdateRover(rover);

                if (_geofence.IsOutside(rover.Id, rover.Position))
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
            }

            _frameCount++;
            FrameCounterText.Text = _frameCount + " frames";

            if (ReferenceEquals(rover, _vm.SelectedRover))
            {
                _vm.RefreshSelectedReadouts();
                UpdateDriveDisplay();
            }
        }

        private void DriveTimer_Tick(object sender, EventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null || _commands == null) return;

            DriveController drive = _driveControllers.For(rover.Id);
            bool wasLatched = drive.IsEmergencyStopLatched;

            StationCommand command = drive.NextDriveCommand(rover.Id, rover.IsEmergencyStopped, rover.LastFrameUtc,
                                                             DateTime.UtcNow,
                                                             (short)ThrottleSlider.Value,
                                                             (short)SteeringSlider.Value);

            // The latch just adopted a stop the station didn't know about,
            // rather than clearing it (#37). Reflect that on screen
            // immediately rather than waiting for the next frame.
            if (!wasLatched && drive.IsEmergencyStopLatched)
            {
                UpdateDriveDisplay();
                Log.Warn(rover.Name + " reports an emergency stop the station was not holding. " +
                        "Treating it as latched until re-armed.");
            }

            _commands.Send(rover.Id, command.Throttle, command.Steering,
                           command.EmergencyStop, command.Armed);
        }

        private void Arm_Click(object sender, RoutedEventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null)
            {
                MessageBox.Show("Select a rover first.", "RoverRally", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_commands == null)
            {
                Log.Error("Arm request for " + rover.Name + " ignored: the command link is not running.");
                MessageBox.Show("The command link is not running, so nothing was sent.",
                                "RoverRally", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DriveController drive = _driveControllers.For(rover.Id);

            // Captures whether a stop was in force before the call, whether
            // the station already knew about it or is only now finding out
            // from the vehicle's own telemetry - both count as "cleared" below.
            bool stopWasHeld = drive.IsEmergencyStopLatched ||
                              DriveController.VehicleReportsStopped(rover.IsEmergencyStopped, rover.LastFrameUtc, DateTime.UtcNow);

            StationCommand command;
            if (!drive.TryToggleArm(rover.Id, rover.IsEmergencyStopped, rover.LastFrameUtc, DateTime.UtcNow,
                                     (short)ThrottleSlider.Value, out command))
            {
                Log.Warn("Re-arm of " + rover.Name + " refused: the throttle is not centred.");
                MessageBox.Show("Centre the throttle before re-arming " + rover.Name + ".",
                                "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateDriveDisplay();

            _commands.Send(rover.Id, command.Throttle, command.Steering,
                           command.EmergencyStop, command.Armed);

            if (stopWasHeld && !drive.IsEmergencyStopLatched)
            {
                Log.Info("Cleared the emergency stop on " + rover.Name + " via re-arm.");
            }

            Log.Info((drive.IsArmed ? "Armed " : "Disarmed ") + rover.Name + ".");
        }

        private void EmergencyStop_Click(object sender, RoutedEventArgs e)
        {
            Rover rover = _vm.SelectedRover;
            if (rover == null) return;

            // A stop that quietly does nothing is worse than no button at all,
            // so say so rather than throwing or returning in silence.
            if (_commands == null)
            {
                Log.Error("EMERGENCY STOP for " + rover.Name + " could not be sent: the command link is not running.");
                MessageBox.Show("The command link is not running, so the emergency stop was NOT sent." +
                                Environment.NewLine + Environment.NewLine +
                                "Stop " + rover.Name + " by hand.",
                                "RoverRally", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Deliberately not gated on _telemetry.IsReconnecting. That flag
            // describes the inbound telemetry socket; commands leave through a
            // separate socket owned by CommandSender and are unaffected by it.
            // Refusing to stop an eleven-kilo vehicle because an unrelated
            // listener is rebinding is not a trade this station gets to make.
            StationCommand command = _driveControllers.For(rover.Id).EngageEmergencyStop();
            _commands.Send(rover.Id, command.Throttle, command.Steering,
                           command.EmergencyStop, command.Armed);

            // The stop disarms, so the button is now the re-arm.
            UpdateDriveDisplay();

            Log.Warn("Emergency stop sent to " + rover.Name + ". The station will hold it until re-arm.");

            string message = "Emergency stop sent to " + rover.Name + "." +
                             Environment.NewLine + Environment.NewLine +
                             "The stop is held until you press ARM.";

            if (_telemetry != null && _telemetry.IsReconnecting)
            {
                message += Environment.NewLine + Environment.NewLine +
                           "Telemetry is reconnecting, so the readouts may lag. " +
                           "The stop was still sent.";
            }

            MessageBox.Show(message, "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Shows the station's latch beside the vehicle's own reported state,
        /// and keeps the ARM/DISARM label in step with the same controller.
        /// They should agree within a frame or two; a station asserting a stop
        /// that the vehicle is not reporting back is exactly the failure this
        /// control exists to make visible. As of #35 "the station's latch"
        /// means the selected rover's own DriveController, not a station-wide
        /// flag that could describe a different vehicle - both the text and
        /// the button are read from it here so they can never drift apart.
        /// </summary>
        private void UpdateDriveDisplay()
        {
            Rover rover = _vm.SelectedRover;
            DriveController drive = rover == null ? null : _driveControllers.For(rover.Id);

            ArmButton.Content = drive != null && drive.IsArmed ? "DISARM" : "ARM";

            string station = drive == null
                ? "STATION: DISARMED"
                : drive.IsEmergencyStopLatched
                    ? "STATION: STOP LATCHED"
                    : (drive.IsArmed ? "STATION: ARMED" : "STATION: DISARMED");

            string vehicle;
            if (rover == null || rover.LastFrameUtc == DateTime.MinValue) vehicle = "VEHICLE: NO DATA";
            else if (rover.IsEmergencyStopped) vehicle = "VEHICLE: STOPPED";
            else vehicle = rover.IsArmed ? "VEHICLE: ARMED" : "VEHICLE: DISARMED";

            if (_latchedStateBrush == null) _latchedStateBrush = (Brush)FindResource("DangerBrush");

            DriveStateText.Text = station + "  -  " + vehicle;
            DriveStateText.Foreground = (drive != null && drive.IsEmergencyStopLatched) || (rover != null && rover.IsEmergencyStopped)
                ? _latchedStateBrush
                : QuietStateBrush;
        }

        private static Brush CreateQuietStateBrush()
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(0x6E, 0x77, 0x87));
            brush.Freeze();
            return brush;
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
