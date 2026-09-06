using System.Windows;
using Microsoft.Win32;

namespace RoverRally.App.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            MessageBox.Show(message, title, button, icon);
        }

        public string? ShowSaveFileDialog(string filter, string defaultFileName, string initialDirectory)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName,
                InitialDirectory = initialDirectory
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
