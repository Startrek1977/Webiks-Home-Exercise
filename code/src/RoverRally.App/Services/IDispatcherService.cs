using System;

namespace RoverRally.App.Services
{
    /// <summary>
    /// Wraps the one WPF call <see cref="ViewModels.StationViewModel"/> needs
    /// to marshal telemetry-thread work onto the UI thread - a contract
    /// specifically so a test can invoke the action inline instead of going
    /// through <c>Application.Current.Dispatcher</c>, which is null outside a
    /// running WPF application (including under a test runner) and would
    /// throw a NullReferenceException the moment a test tried to exercise
    /// the telemetry event handlers directly - exactly the case #73 exists
    /// to make reachable from a test.
    /// </summary>
    public interface IDispatcherService
    {
        void Invoke(Action action);
    }
}
