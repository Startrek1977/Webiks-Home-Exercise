using System.Windows;

namespace RoverRally.App
{
    /// <summary>
    /// Zero code-behind by design (#73): the DataContext is a
    /// <see cref="ViewModels.StationViewModel"/> assigned externally by the
    /// composition root (<see cref="App"/>), not constructed or referenced
    /// here. Window lifecycle events (Loaded, Closing) are wired to
    /// ViewModel commands via XAML behaviors instead of event handlers.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
