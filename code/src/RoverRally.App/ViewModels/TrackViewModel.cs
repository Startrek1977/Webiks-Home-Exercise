using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoverRally.App.Services;
using RoverRally.App.Views;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
using RoverRally.Core.Geo;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;
using RoverRally.Core.Units;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The Track tab (#88): drive-safety control (arm/disarm, emergency stop),
    /// the lap timer, geofence display, and the selected rover's live
    /// telemetry readout. Reaches the roster/selection all three tabs share
    /// via <see cref="RoverFleetState"/> and the operator's speed-unit
    /// preference via <see cref="SpeedUnitState"/> - both standalone DI
    /// singletons, so this view model never references <c>FleetViewModel</c>
    /// or <c>SettingsViewModel</c> directly.
    /// </summary>
    public partial class TrackViewModel : ObservableObject
    {
        private readonly IStationService _service;
        private readonly IDriveControllerRegistry _driveControllers;
        private readonly IDialogService _dialogService;
        private readonly RoverFleetState _fleetState;
        private readonly SpeedUnitState _speedUnitState;

        private readonly HashSet<byte> _fencedAlerted = new HashSet<byte>();

        [ObservableProperty]
        private int _lapCount;

        [ObservableProperty]
        private string _lastLapDisplay = "--";

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

        public TrackViewModel(IStationService service, IDriveControllerRegistry driveControllers,
                              IDialogService dialogService, RoverFleetState fleetState, SpeedUnitState speedUnitState)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _driveControllers = driveControllers ?? throw new ArgumentNullException(nameof(driveControllers));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _fleetState = fleetState ?? throw new ArgumentNullException(nameof(fleetState));
            _speedUnitState = speedUnitState ?? throw new ArgumentNullException(nameof(speedUnitState));

            _service.DriveTimerTick += DriveTimer_Tick;
            _fleetState.PropertyChanged += FleetState_PropertyChanged;
            _speedUnitState.PropertyChanged += SpeedUnitState_PropertyChanged;
        }

        public ObservableCollection<Rover> Rovers => _fleetState.Rovers;

        public Rover? SelectedRover
        {
            get => _fleetState.SelectedRover;
            set => _fleetState.SelectedRover = value;
        }

        public bool HasSelection => SelectedRover != null;

        public string SelectedSpeedDisplay =>
            SelectedRover == null ? "--" : SpeedConverter.Format(SelectedRover.SpeedCmS, _speedUnitState.Unit);

        public string ArmButtonText { get => _armButtonText; private set => SetProperty(ref _armButtonText, value); }
        public string DriveStateText { get => _driveStateText; private set => SetProperty(ref _driveStateText, value); }
        public bool DriveStateIsLatched { get => _driveStateIsLatched; private set => SetProperty(ref _driveStateIsLatched, value); }

        public double TrackNorth { get => _trackNorth; private set => SetProperty(ref _trackNorth, value); }
        public double TrackSouth { get => _trackSouth; private set => SetProperty(ref _trackSouth, value); }
        public double TrackWest { get => _trackWest; private set => SetProperty(ref _trackWest, value); }
        public double TrackEast { get => _trackEast; private set => SetProperty(ref _trackEast, value); }
        public TrackPoint[] Geofence { get => _geofence; private set => SetProperty(ref _geofence, value); }

        private void FleetState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(RoverFleetState.SelectedRover)) return;

            OnPropertyChanged(nameof(SelectedRover));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedSpeedDisplay));
            UpdateDriveDisplay();
            UpdateLapDisplay();
        }

        private void SpeedUnitState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpeedUnitState.Unit)) OnPropertyChanged(nameof(SelectedSpeedDisplay));
        }

        public void LoadTrack()
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

        /// <summary>
        /// Called by <see cref="StationViewModel.ApplyFrame"/> for every
        /// frame's rover - handles the geofence alert and lap-timer update,
        /// and refreshes the on-screen readout only if this frame's rover is
        /// the one currently selected.
        /// </summary>
        public void OnFrameApplied(Rover rover, TelemetryFrame frame, DateTime receivedUtc)
        {
            if (!Rovers.Any(r => ReferenceEquals(r, rover) || r.Id == rover.Id)) return;

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

            if (ReferenceEquals(rover, SelectedRover))
            {
                OnPropertyChanged(nameof(SelectedSpeedDisplay));
                UpdateDriveDisplay();
                UpdateLapDisplay();
            }
        }

        private void DriveTimer_Tick(object? sender, EventArgs e)
        {
            Rover? rover = RequireSelection();
            if (rover == null) return;

            DriveController drive = _driveControllers.For(rover.Id);
            bool wasLatched = drive.IsEmergencyStopLatched;

            StationCommand command = drive.NextInstruction(rover.Id, rover.IsEmergencyStopped, rover.LastFrameUtc,
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

            SendStationCommand(rover, command);
        }

        [RelayCommand]
        private void Arm()
        {
            Rover? rover = RequireSelection(true, MessageBoxImage.Information);
            if (rover == null)
            {
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

            SendStationCommand(rover, command);

            if (stopWasHeld && !drive.IsEmergencyStopLatched)
            {
                Log.Info("Cleared the emergency stop on " + rover.Name + " via re-arm.");
            }

            Log.Info((drive.IsArmed ? "Armed " : "Disarmed ") + rover.Name + ".");
        }

        [RelayCommand]
        private void EmergencyStop()
        {
            Rover? rover = RequireSelection(true, MessageBoxImage.Warning,
                                           "Emergency stop pressed with no rover selected; no command sent.");
            if (rover == null) return;

            StationCommand command = _driveControllers.For(rover.Id).EngageEmergencyStop();
            SendStationCommand(rover, command);

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

        private Rover? RequireSelection(bool notifyOperator = false, MessageBoxImage icon = MessageBoxImage.Information,
                                        string? missingSelectionLog = null)
        {
            Rover? rover = SelectedRover;
            if (rover != null) return rover;

            if (missingSelectionLog != null) Log.Warn(missingSelectionLog);

            if (notifyOperator)
            {
                _dialogService.ShowMessage("Select a rover first.", "RoverRally", MessageBoxButton.OK, icon);
            }

            return null;
        }

        private void SendStationCommand(Rover rover, StationCommand command)
        {
            _service.CommandSender.Send(rover.Id, command.Throttle, command.Steering,
                                        command.EmergencyStop, command.Armed);
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

        /// <summary>
        /// Called once by <see cref="StationViewModel.Loaded"/> after the
        /// initial rover selection is resolved, matching the pre-split
        /// behaviour of refreshing these readouts unconditionally at the end
        /// of startup regardless of whether the selection actually changed.
        /// </summary>
        public void RefreshDriveAndLapDisplay()
        {
            UpdateDriveDisplay();
            UpdateLapDisplay();
        }
    }
}
