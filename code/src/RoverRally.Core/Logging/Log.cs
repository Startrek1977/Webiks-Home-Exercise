using System;
using System.IO;
using Microsoft.Extensions.Logging;
using RoverRally.Core.Configuration;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

// The WriteTo.Console()/.Debug()/.File(...) sink extension methods live in
// the bare `Serilog` namespace, so this using is required despite the
// Serilog.Xxx qualification used everywhere else in this file. Safe here
// specifically because nothing in this file ever refers to the bare
// identifier `Log` - it IS the Log class, so its own members are never
// self-qualified - which is what would otherwise collide with Serilog's own
// static `Serilog.Log` class. Serilog also declares its own `ILogger`
// (distinct from Microsoft.Extensions.Logging.ILogger), so that one is
// pinned explicitly to the Microsoft one, which is what this file's public
// API (CreateLogger, the FacadeLogger adapter) is built around.
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace RoverRally.Core.Logging
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// Station log. The static Debug/Info/Warn/Error methods are the facade
    /// every "incidental" call site in the app still uses directly (#22).
    /// <see cref="Configure"/> wires that facade - and every
    /// <see cref="ILogger"/> handed out by <see cref="CreateLogger{T}"/> - to
    /// a real Serilog pipeline: console, the attached debugger, and a
    /// size/day rolling file with bounded retention
    /// (<see cref="FileSizeLimitBytes"/> x <see cref="RetainedFileCountLimit"/>
    /// is the worst-case footprint; see SOLUTION_LOG.md). Until Configure
    /// runs - which almost no unit test does - both paths fall back to the
    /// original Console/Debug-only behaviour rather than throwing.
    ///
    /// Serilog ships its own static <c>Serilog.Log</c> class, so Serilog
    /// types are referenced here by their full namespace rather than via a
    /// <c>using Serilog;</c> that would collide with this class's own name.
    /// </summary>
    public static class Log
    {
        /// <summary>Per-file cap. Combined with <see cref="RetainedFileCountLimit"/>, bounds the total on-disk footprint.</summary>
        public const long FileSizeLimitBytes = 5L * 1024 * 1024;

        /// <summary>Oldest files beyond this count (across both day- and size-rolls) are deleted. Worst case: FileSizeLimitBytes x RetainedFileCountLimit = 50 MB.</summary>
        public const int RetainedFileCountLimit = 10;

        public static LogLevel MinimumLevel = LogLevel.Info;

        private static ILoggerFactory? _loggerFactory;
        private static ILogger? _facadeLogger;
        private static Serilog.Core.Logger? _serilogLogger;

        /// <summary>
        /// Wires the facade and every <see cref="ILogger"/> handed out by
        /// <see cref="CreateLogger{T}"/> to a real Serilog pipeline built
        /// from <paramref name="options"/>. Call once, from
        /// App.xaml.cs.OnStartup. If the rolling file sink can't be opened -
        /// a bad LogDirectory, no permission, a full disk - that failure is
        /// reported to both the console and the attached debugger (a
        /// normally-installed station has no debugger, so the console is
        /// what an operator who launched from a shell actually sees) and
        /// swallowed rather than thrown, so a fault here never takes the
        /// station down; logging simply continues without the file sink.
        /// </summary>
        public static void Configure(StationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            MinimumLevel = options.LogLevel;
            Serilog.Events.LogEventLevel minimumSerilogLevel = ToSerilogLevel(options.LogLevel);

            Serilog.Core.Logger logger;
            try
            {
                logger = BuildLogger(options.LogDirectory, minimumSerilogLevel, includeFileSink: true);
            }
            catch (Exception ex)
            {
                string message = "Could not open the log file under " + options.LogDirectory +
                    "; continuing without file logging. " + ex.Message;
                Console.WriteLine(message);
                System.Diagnostics.Debug.WriteLine(message);
                logger = BuildLogger(options.LogDirectory, minimumSerilogLevel, includeFileSink: false);
            }

            _serilogLogger = logger;
            _loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
            _facadeLogger = _loggerFactory.CreateLogger("Station");
        }

        private static Serilog.Core.Logger BuildLogger(string logDirectory, Serilog.Events.LogEventLevel minimumLevel, bool includeFileSink)
        {
            Serilog.LoggerConfiguration configuration = new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .WriteTo.Console()
                .WriteTo.Debug();

            if (includeFileSink)
            {
                string filePath = Path.Combine(logDirectory, "station-.log");

                configuration = configuration.WriteTo.File(
                    filePath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    fileSizeLimitBytes: FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: RetainedFileCountLimit);
            }

            return configuration.CreateLogger();
        }

        /// <summary>
        /// An <see cref="ILogger"/> for <typeparamref name="T"/>, backed by
        /// whatever <see cref="Configure"/> last set up - or, if Configure
        /// was never called, an adapter that reproduces the original
        /// Console/Debug-only facade behaviour, so callers that skip
        /// Configure (most tests) still work.
        /// </summary>
        public static ILogger CreateLogger<T>()
        {
            return CreateLogger(typeof(T).FullName ?? typeof(T).Name);
        }

        public static ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory != null
                ? _loggerFactory.CreateLogger(categoryName)
                : new FacadeLogger();
        }

        /// <summary>
        /// Flushes and disposes the Serilog pipeline and returns the facade
        /// to its pre-Configure state, including <see cref="MinimumLevel"/>.
        /// Call once, from App.xaml.cs.OnExit - also used by tests that call
        /// <see cref="Configure"/> to restore a clean slate for the tests
        /// that run after them.
        /// </summary>
        public static void Shutdown()
        {
            if (_loggerFactory != null)
            {
                // Built with dispose:false, so this does not cascade into
                // _serilogLogger - that one is disposed explicitly below.
                _loggerFactory.Dispose();
                _loggerFactory = null;
            }

            if (_serilogLogger != null)
            {
                _serilogLogger.Dispose();
                _serilogLogger = null;
            }

            _facadeLogger = null;
            MinimumLevel = LogLevel.Info;
        }

        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public static void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        public static void Error(string message, Exception ex)
        {
            Write(LogLevel.Error, ex == null ? message : message + " -- " + ex.Message);
        }

        private static void Write(LogLevel level, string message)
        {
            if (level < MinimumLevel) return;

            if (_facadeLogger != null)
            {
                _facadeLogger.Log(ToMicrosoftLevel(level), message);
                return;
            }

            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                                        DateTime.Now,
                                        level.ToString().ToUpperInvariant(),
                                        message);

            Console.WriteLine(line);
            System.Diagnostics.Debug.WriteLine(line);
        }

        private static Serilog.Events.LogEventLevel ToSerilogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return Serilog.Events.LogEventLevel.Debug;
                case LogLevel.Warn: return Serilog.Events.LogEventLevel.Warning;
                case LogLevel.Error: return Serilog.Events.LogEventLevel.Error;
                default: return Serilog.Events.LogEventLevel.Information;
            }
        }

        private static MelLogLevel ToMicrosoftLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return MelLogLevel.Debug;
                case LogLevel.Warn: return MelLogLevel.Warning;
                case LogLevel.Error: return MelLogLevel.Error;
                default: return MelLogLevel.Information;
            }
        }

        /// <summary>
        /// Reproduces the pre-#22 Console+Debug-only behaviour for
        /// <see cref="CreateLogger{T}"/> callers that run before - or
        /// without ever calling - <see cref="Configure"/>, which is almost
        /// every unit test. Routes through the same <see cref="Write"/> the
        /// static facade uses, so the two paths never disagree about
        /// formatting or <see cref="MinimumLevel"/> filtering.
        /// </summary>
        private sealed class FacadeLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(MelLogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(MelLogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string message = formatter(state, exception);
                Write(FromMicrosoftLevel(logLevel), exception == null ? message : message + " -- " + exception.Message);
            }

            private static LogLevel FromMicrosoftLevel(MelLogLevel level)
            {
                switch (level)
                {
                    case MelLogLevel.Trace:
                    case MelLogLevel.Debug: return LogLevel.Debug;
                    case MelLogLevel.Warning: return LogLevel.Warn;
                    case MelLogLevel.Error:
                    case MelLogLevel.Critical: return LogLevel.Error;
                    default: return LogLevel.Info;
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
