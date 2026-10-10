namespace ExpenseTrackerPro.Services;

/// <summary>
/// Message boxes and file pickers used by view models. Keeping them behind an interface keeps the view models free
/// of WPF types, so they can be unit tested with a fake.
/// </summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question; returns true only when the user confirms.</summary>
    bool Confirm(string message, string title);

    /// <summary>Lets the user choose an existing file. <paramref name="filter"/> uses the Windows dialog syntax.</summary>
    /// <returns>The chosen path, or null when the dialog was cancelled.</returns>
    string? PickOpenFile(string filter);

    /// <summary>Lets the user choose where to save a file.</summary>
    /// <returns>The chosen path, or null when the dialog was cancelled.</returns>
    string? PickSaveFile(string filter, string defaultName);

    void ShowError(string message);
}
