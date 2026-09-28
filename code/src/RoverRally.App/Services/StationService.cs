using System;
using System.Windows.Threading;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
using RoverRally.Core.Geo;
using RoverRally.Core.Logging;
using RoverRally.Core.Telemetry;

namespace RoverRally.App.Services
{
    public class StationService : IStationService
    {
        private TelemetryClient _telemetry = null!;
        private CommandSender _commands = null!;
        private DispatcherTimer _driveTimer = null!;
        private GeofenceMonitor _geofence = null!;
        private LapTimerRegistry _lapTimers = null!;
        private bool _initialized;

        public TelemetryClient TelemetryClient => _telemetry;
        public CommandSender CommandSender => _commands;
        public GeofenceMonitor Geofence => _geofence;
        public LapTimerRegistry LapTimers => _lapTimers;

        public event EventHandler<TelemetryReceivedEventArgs>? FrameReceived;
        public event EventHandler? ConnectionStateChanged;
        public event EventHandler? DriveTimerTick;

        /// <summary>
        /// Initialize service with geofence and lap timer configuration.
        /// Call Start() afterward to activate the link.
        /// </summary>
        public void Initialize(GeofenceMonitor geofence, LapTimerRegistry lapTimers)
        {
            _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
            _lapTimers = lapTimers ?? throw new ArgumentNullException(nameof(lapTimers));
            _initialized = true;
        }

        public void Start()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize must be called before Start.");

            if (_telemetry != null)
            {
                return;
            }

            _commands = new CommandSender(StationSettings.CommandHost, StationSettings.CommandPort, Log.CreateLogger<CommandSender>());

            _telemetry = new TelemetryClient(Log.CreateLogger<TelemetryClient>());
            _telemetry.FrameReceived += Telemetry_FrameReceived;
            _telemetry.ConnectionStateChanged += Telemetry_ConnectionStateChanged;
            _telemetry.Start(StationSettings.TelemetryPort);

            _driveTimer = new DispatcherTimer();
            _driveTimer.Interval = TimeSpan.FromMilliseconds(StationSettings.DriveCommandIntervalMs);
            _driveTimer.Tick += DriveTimer_Tick;
            _driveTimer.Start();
        }

        public void Stop()
        {
            if (_driveTimer != null) _driveTimer.Stop();
            if (_telemetry != null) _telemetry.Dispose();
            if (_commands != null) _commands.Dispose();
        }

        private void Telemetry_FrameReceived(object? sender, TelemetryReceivedEventArgs e)
        {
            FrameReceived?.Invoke(this, e);
        }

        private void Telemetry_ConnectionStateChanged(object? sender, EventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, e);
        }

        private void DriveTimer_Tick(object? sender, EventArgs e)
        {
            DriveTimerTick?.Invoke(this, e);
        }
    }
}
