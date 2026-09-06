using System.Windows.Controls;

namespace RoverRally.App.Views
{
    /// <summary>
    /// Zero code-behind by design (#73): every column and filter binds
    /// directly to the inherited <see cref="ViewModels.StationViewModel"/>
    /// DataContext. Filtering, selection, and the CSV export used to be
    /// handled here imperatively; that logic now lives on the view model.
    /// </summary>
    public partial class FleetView : UserControl
    {
        public FleetView()
        {
            InitializeComponent();
        }
    }
}
