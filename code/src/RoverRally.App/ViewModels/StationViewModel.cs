using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoverRally.App.Services;
using RoverRally.App.Views;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
using RoverRally.Core.Export;
using RoverRally.Core.Geo;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;
using RoverRally.Core.Roster;
using RoverRally.Core.Session;
using RoverRally.Core.Telemetry;
using RoverRally.Core.Units;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// Owns every piece of station behaviour that used to live in
    /// MainWindow/FleetView/SettingsView code-behind (#73). Takes its
    /// collaborators - <see cref="IStationService"/> and
    /// <see cref="IDriveControllerRegistry"/> - by constructor injection of
    /// their contracts, and never references a View type. Everything a
    /// window's Loaded/Closing events used to trigger imperatively is now a
    /// command (<see cref="LoadedCommand"/>, <see cref="ClosingCommand"/>)
    /// that XAML behaviors invoke, so no code-behind is needed anywhere in
    /// the view layer.
    ///
    /// Built on CommunityToolkit.Mvvm's <see cref="ObservableObject"/> and
    /// its <c>[ObservableProperty]</c>/<c>[RelayCommand]</c> generators
    /// rather than a hand-rolled INotifyPropertyChanged, now that the
    /// toolkit is already a dependency (it was previously used only for
    /// RelayCommand, an inconsistency fixed here). This project floats to
    /// the SDK's C# 12 default on net8.0-windows, so properties use the
    /// annotated-field syntax rather than C# 13's partial-property syntax.
    /// A property that is <c>private set</c> here is <c>private set</c>
    /// because only this view model should ever write it (view bindings are
    /// one-way) - those stay hand-written, calling the inherited
    /// <see cref="ObservableObject.SetProperty{T}(ref T, T, string?)"/>
    /// directly, rather than being generated public.
    /// </summary>
    public partial class StationViewModel : ObservableObject
    {
        private readonly IStationService _service;
        private readonly IDriveControllerRegistry _driveControllers;
        private readonly IDialogService _dialogService;
        private readonly IDispatcherService _dispatcher;

        private readonly HashSet<byte> _fencedAlerted = new HashSet<byte>();
        private readonly ICollectionView _roversView;
        private IList<SessionCacheRecord> _historyRecords = Array.Empty<SessionCacheRecord>();
        private int _frameCount;
        private bool _linkStarted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyPropertyChangedFor(nameof(SelectedSpeedDisplay))]
        private Rover? _selectedRover;

        [ObservableProperty]
        private string _linkState = "Disconnected";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedSpeedDisplay))]
        private SpeedUnit _speedUnit = SpeedUnit.KilometresPerHour;

        [ObservableProperty]
        private int _lapCount;

        [ObservableProperty]
        private string _lastLapDisplay = "--";

        private string _frameCounterDisplay = "0 frames";
        private string _armButtonText = "ARM";
        private string _driveStateText = "STATION: DISARMED  -  VEHICLE: NO DATA";
        private bool _driveStateIsLatched;

        [ObservableProperty]
        private double _throttleValue;

        [ObservableProperty]
        private double _steeringValue;

        private double _trackNorth;
        private double _trackSouth;
        private double _trackWest;
        private double _trackEast;
        private TrackPoint[] _geofence = Array.Empty<TrackPoint>();

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        private string _telemetryPortDisplay = string.Empty;
        private string _commandPortDisplay = string.Empty;
        private string _rosterPathDisplay = string.Empty;
        private string _sessionCacheDisplay = string.Empty;
        private string _trackExtentDisplay = string.Empty;
        private string _logLevelDisplay = string.Empty;
        private string _stationDisplay = string.Empty;

        public StationViewModel(IStationService service, IDriveControllerRegistry driveControllers,
                                IDialogService dialogService, IDispatcherService dispatcher)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _driveControllers = driveControllers ?? throw new ArgumentNullException(nameof(driveControllers));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            Rovers = new ObservableCollection<Rover>();
            HistoryRows = new ObservableCollection<HistoryRowViewModel>();
            StatusFilterOptions = new ObservableCollection<string> { "All", "Offline", "Idle", "Driving", "Charging", "Stopped" };

            _roversView = CollectionViewSource.GetDefaultView(Rovers);
            _roversView.Filter = FilterRover;

            // Subscribed here, not in StartLink(), so a duplicate Loaded
            // firing (Window.Loaded can fire more than once in WPF) can
            // never double-subscribe these - a constructor runs exactly
            // once per instance. StartLink()'s own _linkStarted guard only
            // has to protect IStationService.Start() itself from running
            // twice.
            _service.FrameReceived += Telemetry_FrameReceived;
            _service.ConnectionStateChanged += Telemetry_ConnectionStateChanged;
            _service.DriveTimerTick += DriveTimer_Tick;
        }

        public ObservableCollection<Rover> Rovers { get; }
        public ObservableCollection<HistoryRowViewModel> HistoryRows { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }
        public ICollectionView RoversView => _roversView;

        public bool HasSelection => SelectedRover != null;

        public string SelectedSpeedDisplay =>
            SelectedRover == null ? "--" : SpeedConverter.Format(SelectedRover.SpeedCmS, SpeedUnit);

        public string FrameCounterDisplay
        {
            get => _frameCounterDisplay;
            private set => SetProperty(ref _frameCounterDisplay, value);
        }

        public string ArmButtonText
        {
            get => _armButtonText;
            private set => SetProperty(ref _armButtonText, value);
        }

        public string DriveStateText
        {
            get => _driveStateText;
            private set => SetProperty(ref _driveStateText, value);
        }

        public bool DriveStateIsLatched
        {
            get => _driveStateIsLatched;
            private set => SetProperty(ref _driveStateIsLatched, value);
        }

        public double TrackNorth
        {
            get => _trackNorth;
            private set => SetProperty(ref _trackNorth, value);
        }

        public double TrackSouth
        {
            get => _trackSouth;
            private set => SetProperty(ref _trackSouth, value);
        }

        public double TrackWest
        {
            get => _trackWest;
            private set => SetProperty(ref _trackWest, value);
        }

        public double TrackEast
        {
            get => _trackEast;
            private set => SetProperty(ref _trackEast, value);
        }

        public TrackPoint[] Geofence
        {
            get => _geofence;
            private set => SetProperty(ref _geofence, value);
        }

        public string TelemetryPortDisplay
        {
            get => _telemetryPortDisplay;
            private set => SetProperty(ref _telemetryPortDisplay, value);
        }

        public string CommandPortDisplay
        {
            get => _commandPortDisplay;
            private set => SetProperty(ref _commandPortDisplay, value);
        }

        public string RosterPathDisplay
        {
            get => _rosterPathDisplay;
            private set => SetProperty(ref _rosterPathDisplay, value);
        }

        public string SessionCacheDisplay
        {
            get => _sessionCacheDisplay;
            private set => SetProperty(ref _sessionCacheDisplay, value);
        }

        public string TrackExtentDisplay
        {
            get => _trackExtentDisplay;
            private set => SetProperty(ref _trackExtentDisplay, value);
        }

        public string LogLevelDisplay
        {
            get => _logLevelDisplay;
            private set => SetProperty(ref _logLevelDisplay, value);
        }

        public string StationDisplay
        {
            get => _stationDisplay;
            private set => SetProperty(ref _stationDisplay, value);
        }

        /// <summary>
        /// Nudges the readouts that are computed from the selected rover rather
        /// than bound straight to it.
        /// </summary>
        public void RefreshSelectedReadouts()
        {
            OnPropertyChanged(nameof(SelectedSpeedDisplay));
        }

        partial void OnSelectedRoverChanged(Rover? value)
        {
            UpdateDriveDisplay();
            UpdateLapDisplay();
        }

        partial void OnFilterTextChanged(string value)
        {
            _roversView.Refresh();
        }

        partial void OnSelectedStatusFilterChanged(string value)
        {
            _roversView.Refresh();
        }

        [RelayCommand]
        private void Loaded()
        {
            LoadRoster();
            LoadTrack();
            LoadSessionHistory();
            LoadSettingsDisplay();
            StartLink();

            StationSettings.CleanUpAbandonedProfileKeys();
            SpeedUnit = StationSettings.PreferredSpeedUnit;

            int lastRover = StationSettings.LastSelectedRoverId;
            if (lastRover > 0)
            {
                SelectedRover = Rovers.FirstOrDefault(r => r.Id == lastRover);
            }

            if (SelectedRover == null && Rovers.Count > 0)
            {
                SelectedRover = Rovers[0];
            }

            UpdateDriveDisplay();
            UpdateLapDisplay();
        }

        [RelayCommand]
        private void Closing()
        {
            if (SelectedRover != null)
            {
                StationSettings.LastSelectedRoverId = SelectedRover.Id;
            }

            StationSettings.PreferredSpeedUnit = SpeedUnit;

            _service.Stop();
        }

        private void LoadRoster()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StationSettings.RosterPath);

            foreach (Rover rover in RoverRoster.Load(path))
            {
                Rovers.Add(rover);
            }
        }

        private void LoadTrack()
        {
            TrackNorth = StationSettings.TrackNorth;
            TrackSouth = StationSettings.TrackSouth;
            TrackWest = StationSettings.TrackWest;
            TrackEast = StationSettings.TrackEast;

            TrackPoint[] fence = new TrackPoint[]
            {
                new TrackPoint(TrackNorth - 0.0004, TrackWest + 0.0006),
                new TrackPoint(TrackNorth - 0.0004, TrackEast - 0.0006),
                new TrackPoint(TrackSouth + 0.0004, TrackEast - 0.0006),
                new TrackPoint(TrackSouth + 0.0004, TrackWest + 0.0006)
            };

            Geofence = fence;
            GeofenceMonitor geofence = new GeofenceMonitor(fence);

            // The start line's real-world position is derived from the exact
            // pixels of the "Start / finish" Rectangle TrackView draws, via
            // the same projection the track itself uses. MapMetrics is the
            // neutral ground both this view model and TrackView reference,
            // so neither needs to know about the other (#73).
            TrackProjection projection = new TrackProjection(TrackNorth, TrackSouth, TrackWest, TrackEast,
                                                              MapMetrics.CanvasWidth, MapMetrics.CanvasHeight);
            TrackPoint lineNorth = projection.Unproject(MapMetrics.StartLineX, MapMetrics.StartLineTopY);
            TrackPoint lineSouth = projection.Unproject(MapMetrics.StartLineX, MapMetrics.StartLineBottomY);
            LapTimerRegistry lapTimers = new LapTimerRegistry(lineNorth, lineSouth);

            _service.Initialize(geofence, lapTimers);
        }

        private void LoadSessionHistory()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StationSettings.SessionCachePath);

            try
            {
                SessionCacheMigrator.MigrateIfNeeded(path);
            }
            catch (Exception ex)
            {
                Log.Error("Could not migrate the session cache", ex);
            }

            try
            {
                SetHistory(SessionCacheFile.Read(path));
            }
            catch (Exception ex)
            {
                Log.Error("Could not load the session cache", ex);
            }
        }

        private void SetHistory(IList<SessionCacheRecord> records)
        {
            _historyRecords = records;

            HistoryRows.Clear();
            foreach (SessionCacheRecord record in records.Reverse())
            {
                HistoryRows.Add(new HistoryRowViewModel(record, NameFor(record.RoverId)));
            }
        }

        private void LoadSettingsDisplay()
        {
            TelemetryPortDisplay = StationSettings.TelemetryPort.ToString();
            CommandPortDisplay = StationSettings.CommandPort.ToString();
            RosterPathDisplay = StationSettings.RosterPath;
            SessionCacheDisplay = StationSettings.SessionCachePath;
            TrackExtentDisplay = string.Format("N {0:0.0000}  S {1:0.0000}  W {2:0.0000}  E {3:0.0000}",
                                               StationSettings.TrackNorth, StationSettings.TrackSouth,
                                               StationSettings.TrackWest, StationSettings.TrackEast);
            LogLevelDisplay = Log.MinimumLevel.ToString();
            StationDisplay = App.StationName + "  (operator " + App.OperatorName + ")";
        }

        private void StartLink()
        {
            // Window.Loaded (and so LoadedCommand) can fire more than once in
            // WPF - e.g. if the shell is ever removed and re-added to the
            // visual tree. Event subscriptions live in the constructor (runs
            // exactly once) so they can't double up; this guard is what
            // stops a second call from leaking the first IStationService.Start()
            // call's TelemetryClient/CommandSender/DispatcherTimer.
            if (_linkStarted) return;
            _linkStarted = true;

            LinkState = "Listening";

            _service.Start();
        }

        private void Telemetry_ConnectionStateChanged(object? sender, EventArgs e)
        {
            _dispatcher.Invoke(delegate
            {
                LinkState = _service.TelemetryClient.IsReconnecting ? "Reconnecting" : "Listening";
            });
        }

        private void Telemetry_FrameReceived(object? sender, TelemetryReceivedEventArgs e)
        {
            DateTime receivedUtc = e.ReceivedUtc;
            _dispatcher.Invoke(delegate { ApplyFrame(e.Frame, receivedUtc); });
        }

        private void ApplyFrame(TelemetryFrame frame, DateTime receivedUtc)
        {
            Rover? rover = Rovers.FirstOrDefault(r => r.Id == frame.RoverId);
            if (rover == null) return;

            rover.ApplyFrame(frame, receivedUtc);

            if (frame.HasGpsFix)
            {
                if (_service.Geofence.IsOutside(rover.Id, rover.Position))
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

                LapTimer lap = _service.LapTimers.For(rover.Id);
                if (lap.Update(rover.Position, receivedUtc))
                {
                    Log.Info(rover.Name + " completed lap " + lap.LapCount + " in " +
                            lap.LastLapTime!.Value.TotalSeconds.ToString("0.0") + "s.");
                }
            }

            _frameCount++;
            FrameCounterDisplay = _frameCount + " frames";

            if (ReferenceEquals(rover, SelectedRover))
            {
                RefreshSelectedReadouts();
                UpdateDriveDisplay();
                UpdateLapDisplay();
            }
        }

        private void DriveTimer_Tick(object? sender, EventArgs e)
        {
            Rover? rover = SelectedRover;
            if (rover == null) return;

            DriveController drive = _driveControllers.For(rover.Id);
            bool wasLatched = drive.IsEmergencyStopLatched;

            StationCommand command = drive.NextDriveCommand(rover.Id, rover.IsEmergencyStopped, rover.LastFrameUtc,
                                                             DateTime.UtcNow,
                                                             (short)ThrottleValue,
                                                             (short)SteeringValue);

            // The latch just adopted a stop the station didn't know about,
            // rather than clearing it (#37). Reflect that on screen
            // immediately rather than waiting for the next frame.
            if (!wasLatched && drive.IsEmergencyStopLatched)
            {
                UpdateDriveDisplay();
                Log.Warn(rover.Name + " reports an emergency stop the station was not holding. " +
                        "Treating it as latched until re-armed.");
            }

            _service.CommandSender.Send(rover.Id, command.Throttle, command.Steering,
                                        command.EmergencyStop, command.Armed);
        }

        [RelayCommand]
        private void Arm()
        {
            Rover? rover = SelectedRover;
            if (rover == null)
            {
                _dialogService.ShowMessage("Select a rover first.", "RoverRally", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DriveController drive = _driveControllers.For(rover.Id);

            bool stopWasHeld = drive.IsEmergencyStopLatched ||
                              DriveController.VehicleReportsStopped(rover.IsEmergencyStopped, rover.LastFrameUtc, DateTime.UtcNow);

            StationCommand? command;
            if (!drive.TryToggleArm(rover.Id, rover.IsEmergencyStopped, rover.LastFrameUtc, DateTime.UtcNow,
                                     (short)ThrottleValue, out command))
            {
                Log.Warn("Re-arm of " + rover.Name + " refused: the throttle is not centred.");
                _dialogService.ShowMessage("Centre the throttle before re-arming " + rover.Name + ".",
                                "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateDriveDisplay();

            _service.CommandSender.Send(rover.Id, command.Throttle, command.Steering,
                                        command.EmergencyStop, command.Armed);

            if (stopWasHeld && !drive.IsEmergencyStopLatched)
            {
                Log.Info("Cleared the emergency stop on " + rover.Name + " via re-arm.");
            }

            Log.Info((drive.IsArmed ? "Armed " : "Disarmed ") + rover.Name + ".");
        }

        [RelayCommand]
        private void EmergencyStop()
        {
            Rover? rover = SelectedRover;
            if (rover == null) return;

            StationCommand command = _driveControllers.For(rover.Id).EngageEmergencyStop();
            _service.CommandSender.Send(rover.Id, command.Throttle, command.Steering,
                                       command.EmergencyStop, command.Armed);

            UpdateDriveDisplay();

            Log.Warn("Emergency stop sent to " + rover.Name + ". The station will hold it until re-arm.");

            string message = "Emergency stop sent to " + rover.Name + "." +
                             Environment.NewLine + Environment.NewLine +
                             "The stop is held until you press ARM.";

            if (_service.TelemetryClient.IsReconnecting)
            {
                message += Environment.NewLine + Environment.NewLine +
                           "Telemetry is reconnecting, so the readouts may lag. " +
                           "The stop was still sent.";
            }

            _dialogService.ShowMessage(message, "RoverRally", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        [RelayCommand]
        private void SetSpeedUnit(SpeedUnit unit)
        {
            SpeedUnit = unit;
        }

        /// <summary>
        /// Exports the completed-run history (session cache) to CSV - not
        /// the live Fleet grid, which is a per-frame snapshot with nothing
        /// retained to export. An empty or absent history shows a message
        /// and skips the save dialog rather than writing a header-only file.
        /// </summary>
        [RelayCommand]
        private void ExportHistory()
        {
            if (_historyRecords.Count == 0)
            {
                _dialogService.ShowMessage("There are no completed runs to export yet.", "Export Run History",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? selectedPath = _dialogService.ShowSaveFileDialog(
                "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                "RoverRally-Runs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            if (selectedPath == null) return;

            try
            {
                string csv = RunHistoryCsvExporter.BuildCsv(_historyRecords, NameFor);
                File.WriteAllText(selectedPath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Failed to export run history to " + selectedPath, ex);
                _dialogService.ShowMessage("Could not save the export file: " + ex.Message, "Export Run History",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string NameFor(int roverId)
        {
            Rover? match = Rovers.FirstOrDefault(r => r.Id == roverId);
            return match == null ? "Rover " + roverId : match.Name ?? ("Rover " + roverId);
        }

        private bool FilterRover(object item)
        {
            Rover? rover = item as Rover;
            if (rover == null) return false;

            if (SelectedStatusFilter != "All" && rover.Status.ToString() != SelectedStatusFilter) return false;

            // Trimmed for matching only, same as the code-behind's SearchBox.Text.Trim()
            // this replaced - FilterText itself keeps whatever the operator typed,
            // including surrounding whitespace, since it's bound straight to the textbox.
            string search = FilterText.Trim();
            if (search.Length > 0)
            {
                bool matchesName = rover.Name != null &&
                                   rover.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesChassis = rover.ChassisType != null &&
                                      rover.ChassisType.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matchesName && !matchesChassis) return false;
            }

            return true;
        }

        /// <summary>
        /// Shows the station's latch beside the vehicle's own reported state,
        /// and keeps the ARM/DISARM label in step with the same controller.
        /// </summary>
        private void UpdateDriveDisplay()
        {
            Rover? rover = SelectedRover;
            DriveController? drive = rover == null ? null : _driveControllers.For(rover.Id);

            ArmButtonText = drive != null && drive.IsArmed ? "DISARM" : "ARM";

            string station = drive == null
                ? "STATION: DISARMED"
                : drive.IsEmergencyStopLatched
                    ? "STATION: STOP LATCHED"
                    : (drive.IsArmed ? "STATION: ARMED" : "STATION: DISARMED");

            string vehicle;
            if (rover == null || rover.LastFrameUtc == DateTime.MinValue) vehicle = "VEHICLE: NO DATA";
            else if (rover.IsEmergencyStopped) vehicle = "VEHICLE: STOPPED";
            else vehicle = rover.IsArmed ? "VEHICLE: ARMED" : "VEHICLE: DISARMED";

            DriveStateText = station + "  -  " + vehicle;
            DriveStateIsLatched = (drive != null && drive.IsEmergencyStopLatched) || (rover != null && rover.IsEmergencyStopped);
        }

        /// <summary>
        /// Shows the selected rover's own lap count and last lap time.
        /// </summary>
        private void UpdateLapDisplay()
        {
            Rover? rover = SelectedRover;
            LapTimer? lap = rover == null ? null : _service.LapTimers.For(rover.Id);

            LapCount = lap == null ? 0 : lap.LapCount;
            LastLapDisplay = lap != null && lap.LastLapTime.HasValue
                ? lap.LastLapTime.Value.TotalSeconds.ToString("0.0") + "s"
                : "--";
        }
    }
}
