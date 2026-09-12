using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using RoverRally.Core.Models;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The rover roster and current selection - genuinely cross-cutting
    /// state that <see cref="StationViewModel"/>, <see cref="TrackViewModel"/>,
    /// and <see cref="FleetViewModel"/> all need to agree on (#88). A
    /// standalone DI singleton rather than a member of any one of those view
    /// models, so none of them has to depend on another to reach it - each
    /// takes this by constructor injection independently, with no circular
    /// dependency between the composition root and its children.
    /// </summary>
    public partial class RoverFleetState : ObservableObject
    {
        public ObservableCollection<Rover> Rovers { get; } = new ObservableCollection<Rover>();

        [ObservableProperty]
        private Rover? _selectedRover;

        public string NameFor(int roverId)
        {
            Rover? match = Rovers.FirstOrDefault(r => r.Id == roverId);
            return match == null ? "Rover " + roverId : match.Name ?? ("Rover " + roverId);
        }
    }
}
