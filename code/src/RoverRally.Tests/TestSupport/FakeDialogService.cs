using System.Collections.Generic;
using System.Windows;
using RoverRally.App.Services;

namespace RoverRally.Tests.TestSupport
{
    /// <summary>
    /// Hand-rolled <see cref="IDialogService"/> test double (#73). Real
    /// MessageBox/SaveFileDialog calls block on a human clicking a real
    /// Windows dialog - fatal to an automated test run. This fake returns
    /// immediately with a scripted answer instead, and records what was
    /// asked so a test can assert on it.
    /// </summary>
    public sealed class FakeDialogService : IDialogService
    {
        public IList<string> Messages { get; } = new List<string>();

        /// <summary>
        /// The path <see cref="ShowSaveFileDialog"/> returns; null simulates
        /// the user cancelling the dialog. Defaults to a non-null path so a
        /// test that doesn't care about the export destination still
        /// exercises the "save" branch rather than the "cancelled" one.
        /// </summary>
        public string? SaveFileDialogResult { get; set; } = "export.csv";

        public void ShowMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Messages.Add(message);
        }

        public string? ShowSaveFileDialog(string filter, string defaultFileName, string initialDirectory)
        {
            return SaveFileDialogResult;
        }
    }
}
