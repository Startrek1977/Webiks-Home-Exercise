using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RoverRally.Core.Models;
using RoverRally.Core.Units;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// Backs the fleet grid and the rover panel. Started out as the place all
    /// the station state was going to live; the drive controls and the link
    /// still talk to the window directly.
    /// </summary>
    public class StationViewModel : INotifyPropertyChanged
    {
        private Rover _selectedRover;
        private string _linkState = "Disconnected";
        private SpeedUnit _speedUnit = SpeedUnit.KilometresPerHour;

        public StationViewModel()
        {
            Rovers = new ObservableCollection<Rover>();
        }

        public ObservableCollection<Rover> Rovers { get; private set; }

        public Rover SelectedRover
        {
            get { return _selectedRover; }
            set
            {
                _selectedRover = value;
                OnPropertyChanged();
                OnPropertyChanged("HasSelection");
                OnPropertyChanged("SelectedSpeedDisplay");
            }
        }

        public bool HasSelection
        {
            get { return _selectedRover != null; }
        }

        public SpeedUnit SpeedUnit
        {
            get { return _speedUnit; }
            set
            {
                _speedUnit = value;
                OnPropertyChanged();
                OnPropertyChanged("SelectedSpeedDisplay");
            }
        }

        public string SelectedSpeedDisplay
        {
            get
            {
                if (_selectedRover == null) return "--";
                return SpeedConverter.Format(_selectedRover.SpeedCmS, _speedUnit);
            }
        }

        public string LinkState
        {
            get { return _linkState; }
            set { _linkState = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Nudges the readouts that are computed from the selected rover rather
        /// than bound straight to it.
        /// </summary>
        public void RefreshSelectedReadouts()
        {
            OnPropertyChanged("SelectedSpeedDisplay");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
