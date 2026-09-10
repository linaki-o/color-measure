namespace ColorMeasure.Domain;

/// <summary>
/// Result of an operation that can fail with a structured error.
/// Avoids exceptions for expected error paths in hot loops (measurement, parsing).
/// </summary>
public readonly record struct Result<T>(bool IsSuccess, T? Value, Error? Error)
{
    public bool IsFailure => !IsSuccess;

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Ok(value);
}

public sealed record Error(string Code, string Message)
{
    public static Error FromSdk(int code, string message) => new($"SDK_{code}", message);
    public static readonly Error Cancelled = new("CANCELLED", "Operation cancelled");
    public static readonly Error ProbeNotConnected = new("PROBE_NOT_CONNECTED", "Probe is not connected");
    public static readonly Error InvalidPlan = new("INVALID_PLAN", "Test plan is invalid");
}
