using System.Windows.Controls;

namespace RoverRally.App.Views
{
    /// <summary>
    /// Zero code-behind by design (#73/#88): every readout and control binds
    /// directly to the <see cref="ViewModels.TrackViewModel"/> DataContext
    /// resolved for this view via the DataTemplate in App.xaml.
    /// </summary>
    public partial class TrackTabView : UserControl
    {
        public TrackTabView()
        {
            InitializeComponent();
        }
    }
}
