using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using RoverRally.App.ViewModels;
using RoverRally.Core.Models;
using RoverRally.Core.Session;

namespace RoverRally.App.Views
{
    public partial class FleetView : UserControl
    {
        private StationViewModel _vm;

        public FleetView()
        {
            InitializeComponent();
        }

        public void Bind(StationViewModel vm)
        {
            _vm = vm;
            FleetGrid.ItemsSource = vm.Rovers;
        }

        public void SetHistory(IList<SessionCacheRecord> records)
        {
            List<HistoryRow> rows = new List<HistoryRow>();

            foreach (SessionCacheRecord record in records.Reverse())
            {
                rows.Add(new HistoryRow(record, NameFor(record.RoverId)));
            }

            HistoryGrid.ItemsSource = rows;
        }

        private string NameFor(int roverId)
        {
            if (_vm == null) return "Rover " + roverId;

            Rover match = _vm.Rovers.FirstOrDefault(r => r.Id == roverId);
            return match == null ? "Rover " + roverId : match.Name;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (_vm == null) return;

            ICollectionView view = CollectionViewSource.GetDefaultView(FleetGrid.ItemsSource);
            if (view == null) return;

            string search = SearchBox.Text == null ? string.Empty : SearchBox.Text.Trim();

            ComboBoxItem selected = StatusFilter.SelectedItem as ComboBoxItem;
            string status = selected == null ? "All" : selected.Content.ToString();

            view.Filter = delegate(object item)
            {
                Rover rover = item as Rover;
                if (rover == null) return false;

                if (status != "All" && rover.Status.ToString() != status) return false;

                if (search.Length > 0)
                {
                    bool matchesName = rover.Name != null &&
                                       rover.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchesChassis = rover.ChassisType != null &&
                                          rover.ChassisType.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchesName && !matchesChassis) return false;
                }

                return true;
            };
        }

        private void FleetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;

            Rover selected = FleetGrid.SelectedItem as Rover;
            if (selected != null) _vm.SelectedRover = selected;
        }

        private class HistoryRow
        {
            public HistoryRow(SessionCacheRecord record, string roverName)
            {
                RoverName = roverName;
                Started = record.StartedUtc.ToString("yyyy-MM-dd HH:mm");
                Duration = record.Duration.ToString(@"hh\:mm\:ss");
                Distance = (record.DistanceCm / 100.0).ToString("0.0") + " m";
                PeakSpeed = (record.PeakSpeedCmS * 0.036).ToString("0.0") + " km/h";
            }

            public string RoverName { get; private set; }
            public string Started { get; private set; }
            public string Duration { get; private set; }
            public string Distance { get; private set; }
            public string PeakSpeed { get; private set; }
        }
    }
}
