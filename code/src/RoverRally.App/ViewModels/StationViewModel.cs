using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoverRally.App.Services;
using RoverRally.Core.Configuration;
using RoverRally.Core.Models;
using RoverRally.Core.Roster;
using RoverRally.Core.Telemetry;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The composition root (#88): owns only what is genuinely cross-cutting
    /// across all three tabs - the link/frame-counter status bar and the
    /// telemetry ingestion pipeline that feeds the shared rover roster - and
    /// exposes <see cref="TrackViewModel"/>, <see cref="FleetViewModel"/>,
    /// and <see cref="SettingsViewModel"/> as properties. Each child view
    /// model owns its own tab's state and commands; none of them reference
    /// each other, reaching shared state instead through the standalone
    /// <see cref="RoverFleetState"/>/<see cref="SpeedUnitState"/> singletons.
    ///
    /// Built on CommunityToolkit.Mvvm's <see cref="ObservableObject"/> and
    /// its <c>[ObservableProperty]</c>/<c>[RelayCommand]</c> generators, same
    /// as before the split (#73).
    /// </summary>
    public partial class StationViewModel : ObservableObject
    {
        private readonly IStationService _service;
        private readonly IDispatcherService _dispatcher;
        private readonly RoverFleetState _fleetState;

        private int _frameCount;
        private bool _linkStarted;

        [ObservableProperty]
        private string _linkState = "Disconnected";

        private string _frameCounterDisplay = "0 frames";

        public StationViewModel(IStationService service, IDispatcherService dispatcher, RoverFleetState fleetState,
                                TrackViewModel trackViewModel, FleetViewModel fleetViewModel, SettingsViewModel settingsViewModel)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _fleetState = fleetState ?? throw new ArgumentNullException(nameof(fleetState));

            TrackViewModel = trackViewModel ?? throw new ArgumentNullException(nameof(trackViewModel));
            FleetViewModel = fleetViewModel ?? throw new ArgumentNullException(nameof(fleetViewModel));
            SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));

            // Subscribed here, not in StartLink(), so a duplicate Loaded
            // firing (Window.Loaded can fire more than once in WPF) can
            // never double-subscribe these - a constructor runs exactly
            // once per instance. StartLink()'s own _linkStarted guard only
            // has to protect IStationService.Start() itself from running
            // twice.
            _service.FrameReceived += Telemetry_FrameReceived;
            _service.ConnectionStateChanged += Telemetry_ConnectionStateChanged;
        }

        public TrackViewModel TrackViewModel { get; }
        public FleetViewModel FleetViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public string FrameCounterDisplay
        {
            get => _frameCounterDisplay;
            private set => SetProperty(ref _frameCounterDisplay, value);
        }

        [RelayCommand]
        private void Loaded()
        {
            LoadRoster();
            TrackViewModel.LoadTrack();
            FleetViewModel.LoadSessionHistory();
            SettingsViewModel.LoadSettingsDisplay();
            StartLink();

            StationSettings.CleanUpAbandonedProfileKeys();
            SettingsViewModel.SpeedUnit = StationSettings.PreferredSpeedUnit;

            int lastRover = StationSettings.LastSelectedRoverId;
            if (lastRover > 0)
            {
                _fleetState.SelectedRover = _fleetState.Rovers.FirstOrDefault(r => r.Id == lastRover);
            }

            if (_fleetState.SelectedRover == null && _fleetState.Rovers.Count > 0)
            {
                _fleetState.SelectedRover = _fleetState.Rovers[0];
            }

            TrackViewModel.RefreshDriveAndLapDisplay();
        }

        [RelayCommand]
        private void Closing()
        {
            if (_fleetState.SelectedRover != null)
            {
                StationSettings.LastSelectedRoverId = _fleetState.SelectedRover.Id;
            }

            StationSettings.PreferredSpeedUnit = SettingsViewModel.SpeedUnit;

            _service.Stop();
        }

        private void LoadRoster()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StationSettings.RosterPath);

            foreach (Rover rover in RoverRoster.Load(path))
            {
                _fleetState.Rovers.Add(rover);
            }
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
            _frameCount++;
            FrameCounterDisplay = _frameCount + " frames";

            Rover? rover = _fleetState.Rovers.FirstOrDefault(r => r.Id == frame.RoverId);
            if (rover == null) return;

            rover.ApplyFrame(frame, receivedUtc);

            TrackViewModel.OnFrameApplied(rover, frame, receivedUtc);
        }
    }
}
