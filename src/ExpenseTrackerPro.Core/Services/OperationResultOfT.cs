namespace ExpenseTrackerPro.Core.Services;

/// <summary>Outcome of an operation that returns a value on success.</summary>
public sealed record OperationResult<T>(bool Success, T? Value, string? Error)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null);

    public static OperationResult<T> Fail(string error) => new(false, default, error);
}
