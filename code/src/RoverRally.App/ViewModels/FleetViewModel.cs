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
using RoverRally.Core.Configuration;
using RoverRally.Core.Export;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;
using RoverRally.Core.Session;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// The Fleet tab (#88): the live grid's name/status filtering and the
    /// completed-run history CSV export. Reaches the roster/selection all
    /// three tabs share via <see cref="RoverFleetState"/> - a standalone DI
    /// singleton, so this view model never references <c>TrackViewModel</c>
    /// or <c>SettingsViewModel</c> directly.
    /// </summary>
    public partial class FleetViewModel : ObservableObject
    {
        private readonly RoverFleetState _fleetState;
        private readonly IDialogService _dialogService;

        private readonly ICollectionView _roversView;
        private IList<SessionCacheRecord> _historyRecords = Array.Empty<SessionCacheRecord>();

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        public FleetViewModel(RoverFleetState fleetState, IDialogService dialogService)
        {
            _fleetState = fleetState ?? throw new ArgumentNullException(nameof(fleetState));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            HistoryRows = new ObservableCollection<HistoryRowViewModel>();
            StatusFilterOptions = new ObservableCollection<string> { "All", "Offline", "Idle", "Driving", "Charging", "Stopped" };

            _roversView = CollectionViewSource.GetDefaultView(_fleetState.Rovers);
            _roversView.Filter = FilterRover;

            _fleetState.PropertyChanged += FleetState_PropertyChanged;
        }

        public ObservableCollection<Rover> Rovers => _fleetState.Rovers;
        public ObservableCollection<HistoryRowViewModel> HistoryRows { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }
        public ICollectionView RoversView => _roversView;

        public Rover? SelectedRover
        {
            get => _fleetState.SelectedRover;
            set => _fleetState.SelectedRover = value;
        }

        private void FleetState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoverFleetState.SelectedRover)) OnPropertyChanged(nameof(SelectedRover));
        }

        partial void OnFilterTextChanged(string value)
        {
            _roversView.Refresh();
        }

        partial void OnSelectedStatusFilterChanged(string value)
        {
            _roversView.Refresh();
        }

        public void LoadSessionHistory()
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
                HistoryRows.Add(new HistoryRowViewModel(record, _fleetState.NameFor(record.RoverId)));
            }
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
                string csv = RunHistoryCsvExporter.BuildCsv(_historyRecords, _fleetState.NameFor);
                File.WriteAllText(selectedPath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Error("Failed to export run history to " + selectedPath, ex);
                _dialogService.ShowMessage("Could not save the export file: " + ex.Message, "Export Run History",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
}
