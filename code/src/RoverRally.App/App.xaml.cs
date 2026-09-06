using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoverRally.App.Services;
using RoverRally.App.ViewModels;
using RoverRally.Core.Configuration;
using RoverRally.Core.Control;
using RoverRally.Core.Logging;

namespace RoverRally.App
{
    public partial class App : Application
    {
        public static string StationName = "RoverRally Station";
        public static string OperatorName = System.Environment.UserName;

        private IServiceProvider? _serviceProvider;

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

            // Composition root: the only place a concrete ViewModel type is
            // constructed and handed to a View. MainWindow's own code never
            // does this - its DataContext is assigned here, and the actual
            // View is resolved by the DataTemplate registered in App.xaml
            // against StationViewModel's type (#73).
            IServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            StationViewModel viewModel = _serviceProvider.GetRequiredService<StationViewModel>();
            MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = viewModel;
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IStationService, StationService>();
            services.AddSingleton<IDriveControllerRegistry, DriveControllerRegistry>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<StationViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Info("Station shutting down.");
            Log.Shutdown();
            base.OnExit(e);
        }
    }
}
