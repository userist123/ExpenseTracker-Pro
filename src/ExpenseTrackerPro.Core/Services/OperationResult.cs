namespace ExpenseTrackerPro.Core.Services;

/// <summary>Outcome of an operation; <see cref="Error"/> holds a Romanian message when it failed.</summary>
public sealed record OperationResult(bool Success, string? Error)
{
    public static OperationResult Ok() => new(true, null);

    public static OperationResult Fail(string error) => new(false, error);
}
