using System.Windows.Controls;

namespace RoverRally.App.Views
{
    /// <summary>
    /// The station's main content, resolved into <see cref="MainWindow"/>'s
    /// shell purely via the DataTemplate registered in App.xaml against
    /// <see cref="ViewModels.StationViewModel"/> (#73) - this class never
    /// constructs or references that view model, and has no code-behind
    /// logic of its own.
    /// </summary>
    public partial class StationView : UserControl
    {
        public StationView()
        {
            InitializeComponent();
        }
    }
}
