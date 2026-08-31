using System;

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
    /// Station log. Everything goes to the process console and to the attached
    /// debugger, which is enough when the station is started from Visual Studio.
    /// </summary>
    public static class Log
    {
        public static LogLevel MinimumLevel = LogLevel.Info;

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

            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}",
                                        DateTime.Now,
                                        level.ToString().ToUpperInvariant(),
                                        message);

            Console.WriteLine(line);
            System.Diagnostics.Debug.WriteLine(line);
        }
    }
}
