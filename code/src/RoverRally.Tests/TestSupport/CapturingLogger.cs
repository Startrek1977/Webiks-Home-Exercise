using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace RoverRally.Tests.TestSupport
{
    /// <summary>
    /// Hand-rolled <see cref="ILogger"/> test double (#22). This repo has no
    /// mocking framework and one class doesn't warrant adding one. Records
    /// every call so a test can assert on level and message without a real
    /// Serilog sink.
    /// </summary>
    public sealed class CapturingLogger : ILogger
    {
        public IList<CapturedEntry> Entries { get; } = new List<CapturedEntry>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedEntry(logLevel, formatter(state, exception), exception));
        }

        public bool HasEntry(LogLevel level, string messageContains)
        {
            foreach (CapturedEntry entry in Entries)
            {
                if (entry.Level == level && entry.Message.Contains(messageContains)) return true;
            }

            return false;
        }

        public sealed class CapturedEntry
        {
            public CapturedEntry(LogLevel level, string message, Exception? exception)
            {
                Level = level;
                Message = message;
                Exception = exception;
            }

            public LogLevel Level { get; }
            public string Message { get; }
            public Exception? Exception { get; }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
