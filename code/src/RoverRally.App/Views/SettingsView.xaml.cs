using System.Windows.Controls;

namespace RoverRally.App.Views
{
    /// <summary>
    /// Zero code-behind by design (#73): every field binds directly to the
    /// inherited <see cref="ViewModels.StationViewModel"/> DataContext.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}
