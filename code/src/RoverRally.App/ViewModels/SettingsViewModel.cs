using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoverRally.Core.Configuration;
using RoverRally.Core.Logging;
using RoverRally.Core.Units;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The Settings tab (#88): read-only display of site configuration, plus
    /// the speed-unit toggle. Owns <see cref="SpeedUnitState"/>'s value (a
    /// standalone DI singleton also read by <see cref="TrackViewModel"/>),
    /// so this view model never references <c>TrackViewModel</c> directly.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SpeedUnitState _speedUnitState;

        private string _telemetryPortDisplay = string.Empty;
        private string _commandPortDisplay = string.Empty;
        private string _rosterPathDisplay = string.Empty;
        private string _sessionCacheDisplay = string.Empty;
        private string _trackExtentDisplay = string.Empty;
        private string _logLevelDisplay = string.Empty;
        private string _stationDisplay = string.Empty;

        public SettingsViewModel(SpeedUnitState speedUnitState)
        {
            _speedUnitState = speedUnitState ?? throw new ArgumentNullException(nameof(speedUnitState));

            _speedUnitState.PropertyChanged += SpeedUnitState_PropertyChanged;
        }

        public SpeedUnit SpeedUnit
        {
            get => _speedUnitState.Unit;
            set => _speedUnitState.Unit = value;
        }

        public string TelemetryPortDisplay { get => _telemetryPortDisplay; private set => SetProperty(ref _telemetryPortDisplay, value); }
        public string CommandPortDisplay { get => _commandPortDisplay; private set => SetProperty(ref _commandPortDisplay, value); }
        public string RosterPathDisplay { get => _rosterPathDisplay; private set => SetProperty(ref _rosterPathDisplay, value); }
        public string SessionCacheDisplay { get => _sessionCacheDisplay; private set => SetProperty(ref _sessionCacheDisplay, value); }
        public string TrackExtentDisplay { get => _trackExtentDisplay; private set => SetProperty(ref _trackExtentDisplay, value); }
        public string LogLevelDisplay { get => _logLevelDisplay; private set => SetProperty(ref _logLevelDisplay, value); }
        public string StationDisplay { get => _stationDisplay; private set => SetProperty(ref _stationDisplay, value); }

        private void SpeedUnitState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpeedUnitState.Unit)) OnPropertyChanged(nameof(SpeedUnit));
        }

        [RelayCommand]
        private void SetSpeedUnit(SpeedUnit unit)
        {
            SpeedUnit = unit;
        }

        public void LoadSettingsDisplay()
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
    }
}
