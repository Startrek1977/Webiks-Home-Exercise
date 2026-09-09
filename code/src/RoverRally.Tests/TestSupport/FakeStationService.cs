using System;
using RoverRally.App.Services;
using RoverRally.Core.Control;
using RoverRally.Core.Geo;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests.TestSupport
{
    /// <summary>
    /// Hand-rolled <see cref="IStationService"/> test double (#73). This repo
    /// has no mocking framework and one class doesn't warrant adding one
    /// (see <see cref="CapturingLogger"/>). <see cref="TelemetryClient"/>/
    /// <see cref="CommandSender"/> are real, unstarted instances - both are
    /// safe to construct and call without a live socket doing anything
    /// (CommandSender.Send swallows send errors; TelemetryClient.IsReconnecting
    /// defaults to false until Start() is called), so a command that reaches
    /// into them during a test behaves exactly as it would with the real
    /// service, without needing a simulator.
    /// </summary>
    public sealed class FakeStationService : IStationService
    {
        public TelemetryClient TelemetryClient { get; } = new TelemetryClient();
        public CommandSender CommandSender { get; } = new CommandSender("127.0.0.1", 0);
        public GeofenceMonitor Geofence { get; private set; } = new GeofenceMonitor(Array.Empty<TrackPoint>());
        public LapTimerRegistry LapTimers { get; private set; } = new LapTimerRegistry(new TrackPoint(0, 0), new TrackPoint(0, 0));

        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public event EventHandler<TelemetryReceivedEventArgs>? FrameReceived;
        public event EventHandler? ConnectionStateChanged;
        public event EventHandler? DriveTimerTick;

        public void Initialize(GeofenceMonitor geofence, LapTimerRegistry lapTimers)
        {
            Geofence = geofence;
            LapTimers = lapTimers;
        }

        public void Start()
        {
            StartCallCount++;
        }

        public void Stop()
        {
            StopCallCount++;
        }

        /// <summary>
        /// Lets a test simulate a decoded frame arriving on the (real, in
        /// production) listener thread, the same way StationService does.
        /// </summary>
        public void RaiseFrameReceived(TelemetryFrame frame)
        {
            FrameReceived?.Invoke(this, new TelemetryReceivedEventArgs(frame));
        }

        public void RaiseConnectionStateChanged()
        {
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseDriveTimerTick()
        {
            DriveTimerTick?.Invoke(this, EventArgs.Empty);
        }
    }
}
