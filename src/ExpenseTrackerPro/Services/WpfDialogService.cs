using System.Windows;
using Microsoft.Win32;

namespace ExpenseTrackerPro.Services;

public sealed class WpfDialogService : IDialogService
{
    private const string AppTitle = "ExpenseTracker Pro";

    public bool Confirm(string message, string title)
    {
        var answer = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return answer == MessageBoxResult.Yes;
    }

    public string? PickOpenFile(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string filter, string defaultName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
