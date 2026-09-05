using System.Windows;
using System.Windows.Controls;
using RoverRally.App.ViewModels;
using RoverRally.Core.Configuration;
using RoverRally.Core.Logging;
using RoverRally.Core.Units;

namespace RoverRally.App.Views
{
    public partial class SettingsView : UserControl
    {
        private StationViewModel? _vm;

        public SettingsView()
        {
            InitializeComponent();
        }

        public void Bind(StationViewModel vm)
        {
            _vm = vm;

            TelemetryPortText.Text = StationSettings.TelemetryPort.ToString();
            CommandPortText.Text = StationSettings.CommandPort.ToString();
            RosterPathText.Text = StationSettings.RosterPath;
            SessionCacheText.Text = StationSettings.SessionCachePath;
            TrackExtentText.Text = string.Format("N {0:0.0000}  S {1:0.0000}  W {2:0.0000}  E {3:0.0000}",
                                                 StationSettings.TrackNorth, StationSettings.TrackSouth,
                                                 StationSettings.TrackWest, StationSettings.TrackEast);
            LogLevelText.Text = Log.MinimumLevel.ToString();
            StationText.Text = App.StationName + "  (operator " + App.OperatorName + ")";

            if (vm.SpeedUnit == SpeedUnit.MilesPerHour) MphOption.IsChecked = true;
            else KmhOption.IsChecked = true;
        }

        private void SpeedUnit_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            _vm.SpeedUnit = MphOption.IsChecked == true
                ? SpeedUnit.MilesPerHour
                : SpeedUnit.KilometresPerHour;
        }
    }
}
