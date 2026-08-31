using System.Configuration;
using System.Windows;
using RoverRally.Core.Logging;

namespace RoverRally.App
{
    public partial class App : Application
    {
        public static string StationName = "RoverRally Station";
        public static string OperatorName = System.Environment.UserName;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string configured = ConfigurationManager.AppSettings["StationName"];
            if (!string.IsNullOrEmpty(configured)) StationName = configured;

            string level = ConfigurationManager.AppSettings["LogLevel"];
            if (!string.IsNullOrEmpty(level))
            {
                LogLevel parsed;
                if (System.Enum.TryParse(level, true, out parsed)) Log.MinimumLevel = parsed;
            }

            Log.Info("Station starting up: " + StationName + " (operator " + OperatorName + ")");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Info("Station shutting down.");
            base.OnExit(e);
        }
    }
}
