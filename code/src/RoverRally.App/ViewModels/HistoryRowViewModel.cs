using RoverRally.Core.Session;

namespace RoverRally.App.ViewModels
{
    /// <summary>
    /// Display-ready projection of one completed run for the history grid.
    /// Moved out of FleetView's code-behind (#73) so the view can bind to it
    /// directly instead of being handed a pre-built row list imperatively.
    /// </summary>
    public class HistoryRowViewModel
    {
        public HistoryRowViewModel(SessionCacheRecord record, string roverName)
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
