using System;
using RoverRally.App.Services;

namespace RoverRally.Tests.TestSupport
{
    /// <summary>
    /// Hand-rolled <see cref="IDispatcherService"/> test double. Invokes the
    /// action inline on the calling thread instead of marshalling through a
    /// real WPF Dispatcher - which doesn't exist under a test runner, since
    /// Application.Current is null outside a running WPF application. Tests
    /// are single-threaded, so running the action immediately is equivalent
    /// to what a real dispatcher would eventually do.
    /// </summary>
    public sealed class FakeDispatcherService : IDispatcherService
    {
        public void Invoke(Action action)
        {
            action();
        }
    }
}
