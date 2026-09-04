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

            StationOptions options = StationOptions.Load(configuration);
            StationSettings.Configure(options);
            Log.Configure(options);

            StationName = StationSettings.StationName;

            Log.Info("Station starting up: " + StationName + " (operator " + OperatorName + ")");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Info("Station shutting down.");
            Log.Shutdown();
            base.OnExit(e);
        }
    }
}
