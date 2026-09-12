using CommunityToolkit.Mvvm.ComponentModel;
using RoverRally.Core.Units;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The operator's chosen speed unit - owned by the Settings tab's toggle
    /// but also read by the Track tab's speed readout (#88). A standalone DI
    /// singleton so <see cref="TrackViewModel"/> and <see cref="SettingsViewModel"/>
    /// can each depend on it independently without either view model
    /// referencing the other.
    /// </summary>
    public partial class SpeedUnitState : ObservableObject
    {
        [ObservableProperty]
        private SpeedUnit _unit = SpeedUnit.KilometresPerHour;
    }
}
