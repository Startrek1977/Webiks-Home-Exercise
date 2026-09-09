using System;
using RoverRally.Core.Control;
using RoverRally.Core.Geo;
using RoverRally.Core.Telemetry;

namespace RoverRally.App.Services
{
    /// <summary>
    /// Owns the station's link-layer services: telemetry client, command sender,
    /// drive timer, geofence monitoring, and lap timing. Raises events that the
    /// window subscribes to in order to update the UI on the main thread.
    /// </summary>
    public interface IStationService
    {
        /// <summary>
        /// Raised on the listener thread when a new telemetry frame arrives.
        /// Subscribers must marshal to the UI thread if they update WPF state.
        /// </summary>
        event EventHandler<TelemetryReceivedEventArgs>? FrameReceived;

        /// <summary>
        /// Raised when the telemetry client connection state changes (listening,
        /// reconnecting, etc.). Subscribers must marshal to the UI thread.
        /// </summary>
        event EventHandler? ConnectionStateChanged;

        /// <summary>
        /// Raised on the dispatcher thread when the drive timer ticks (200 ms
        /// interval). Subscribers execute synchronously on the UI thread.
        /// </summary>
        event EventHandler? DriveTimerTick;

        TelemetryClient TelemetryClient { get; }
        CommandSender CommandSender { get; }
        GeofenceMonitor Geofence { get; }
        LapTimerRegistry LapTimers { get; }

        /// <summary>
        /// Initialize service with geofence and lap timer configuration.
        /// Call Start() afterward to activate the link.
        /// </summary>
        void Initialize(GeofenceMonitor geofence, LapTimerRegistry lapTimers);

        /// <summary>
        /// Starts the telemetry client and drive timer. Called once from MainWindow.Loaded.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the telemetry client and drive timer on shutdown.
        /// </summary>
        void Stop();
    }
}
