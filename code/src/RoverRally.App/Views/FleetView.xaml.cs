using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using RoverRally.App.ViewModels;
using RoverRally.Core.Export;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;
using RoverRally.Core.Session;

namespace RoverRally.App.Views
{
    public partial class FleetView : UserControl
    {
        private StationViewModel? _vm;
        private IList<SessionCacheRecord> _historyRecords = Array.Empty<SessionCacheRecord>();

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
            _historyRecords = records;

            List<HistoryRow> rows = new List<HistoryRow>();

            foreach (SessionCacheRecord record in records.Reverse())
            {
                rows.Add(new HistoryRow(record, NameFor(record.RoverId)));
            }

            HistoryGrid.ItemsSource = rows;
        }

        /// <summary>
        /// Exports the completed-run history (session cache) to CSV - not
        /// the live Fleet grid above, which is a per-frame snapshot with
        /// nothing retained to export. An empty or absent history (fresh
        /// install, missing/empty session-cache.bin) shows a message and
        /// skips the save dialog rather than writing a header-only file.
        /// </summary>
        private void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_historyRecords.Count == 0)
            {
                MessageBox.Show("There are no completed runs to export yet.", "Export Run History",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "RoverRally-Runs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                string csv = RunHistoryCsvExporter.BuildCsv(_historyRecords, NameFor);
                File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Failed to export run history to " + dialog.FileName, ex);
                MessageBox.Show("Could not save the export file: " + ex.Message, "Export Run History",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string NameFor(int roverId)
        {
            if (_vm == null) return "Rover " + roverId;

            Rover? match = _vm.Rovers.FirstOrDefault(r => r.Id == roverId);
            return match == null ? "Rover " + roverId : match.Name ?? ("Rover " + roverId);
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (_vm == null) return;

            ICollectionView view = CollectionViewSource.GetDefaultView(FleetGrid.ItemsSource);
            if (view == null) return;

            string search = SearchBox.Text == null ? string.Empty : SearchBox.Text.Trim();

            ComboBoxItem? selected = StatusFilter.SelectedItem as ComboBoxItem;
            string status = selected == null ? "All" : selected.Content.ToString() ?? "All";

            view.Filter = delegate(object item)
            {
                Rover? rover = item as Rover;
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

            Rover? selected = FleetGrid.SelectedItem as Rover;
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
