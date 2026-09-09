using System.Windows;

namespace RoverRally.App.Services
{
    /// <summary>
    /// Wraps the two WPF dialog calls <see cref="ViewModels.StationViewModel"/>
    /// needs (a message box, a save-file picker) behind a contract, so tests
    /// can supply a fake that returns immediately instead of blocking on a
    /// real modal dialog (#73) - without this, exercising the emergency-stop
    /// or export-history commands' success paths in an automated test would
    /// hang the test runner waiting for a human to click a real Windows
    /// dialog.
    /// </summary>
    public interface IDialogService
    {
        void ShowMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon);

        /// <summary>
        /// Returns the chosen path, or null if the user cancelled.
        /// </summary>
        string? ShowSaveFileDialog(string filter, string defaultFileName, string initialDirectory);
    }
}
