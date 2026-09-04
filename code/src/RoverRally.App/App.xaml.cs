using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using RoverRally.Core.Configuration;
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

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            StationSettings.Configure(StationOptions.Load(configuration));

            StationName = StationSettings.StationName;
            Log.MinimumLevel = StationSettings.LogLevel;

            Log.Info("Station starting up: " + StationName + " (operator " + OperatorName + ")");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Info("Station shutting down.");
            base.OnExit(e);
        }
    }
}
