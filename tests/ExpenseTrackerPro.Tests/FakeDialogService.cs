using ExpenseTrackerPro.Services;

namespace ExpenseTrackerPro.Tests;

/// <summary>Scripted dialogs: answers are set by the test, and every question is recorded.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;

    public string? OpenPath { get; set; }

    public string? SavePath { get; set; }

    public List<(string Message, string Title)> Confirmations { get; } = [];

    public List<string> Errors { get; } = [];

    public List<string> OpenFilters { get; } = [];

    public List<(string Filter, string DefaultName)> SaveRequests { get; } = [];

    public bool Confirm(string message, string title)
    {
        Confirmations.Add((message, title));
        return ConfirmAnswer;
    }

    public string? PickOpenFile(string filter)
    {
        OpenFilters.Add(filter);
        return OpenPath;
    }

    public string? PickSaveFile(string filter, string defaultName)
    {
        SaveRequests.Add((filter, defaultName));
        return SavePath;
    }

    public void ShowError(string message)
    {
        Errors.Add(message);
    }
}
