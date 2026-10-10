namespace ExpenseTrackerPro.Core.Services;

/// <summary>A problem found while importing; <see cref="Message"/> is Romanian.</summary>
public sealed record ImportError(int LineNumber, string Message)
{
    public override string ToString() => $"Linia {LineNumber}: {Message}";
}
