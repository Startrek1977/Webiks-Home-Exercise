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
        private TelemetryClient? _telemetry;
        private CommandSender? _commands;
        private DispatcherTimer? _driveTimer;
        private GeofenceMonitor? _geofence;
        private LapTimerRegistry? _lapTimers;
        private bool _started;

        public TelemetryClient TelemetryClient => _telemetry ?? throw new InvalidOperationException("Start must be called before accessing TelemetryClient.");
        public CommandSender CommandSender => _commands ?? throw new InvalidOperationException("Start must be called before accessing CommandSender.");
        public GeofenceMonitor Geofence => _geofence ?? throw new InvalidOperationException("Initialize must be called before accessing Geofence.");
        public LapTimerRegistry LapTimers => _lapTimers ?? throw new InvalidOperationException("Initialize must be called before accessing LapTimers.");

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
        }

        public void Start()
        {
            if (_geofence == null || _lapTimers == null)
            {
                throw new InvalidOperationException("Initialize must be called before Start.");
            }

            if (_started) return;
            _started = true;

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
            if (!_started) return;
            _started = false;

            if (_driveTimer != null)
            {
                _driveTimer.Stop();
                _driveTimer.Tick -= DriveTimer_Tick;
                _driveTimer = null;
            }

            if (_telemetry != null)
            {
                _telemetry.FrameReceived -= Telemetry_FrameReceived;
                _telemetry.ConnectionStateChanged -= Telemetry_ConnectionStateChanged;
                _telemetry.Dispose();
                _telemetry = null;
            }

            if (_commands != null)
            {
                _commands.Dispose();
                _commands = null;
            }
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
