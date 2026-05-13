namespace Pollmaster.Shared.Common;

/// <summary>
/// Discriminated success/failure container that avoids throwing for expected control-flow.
/// </summary>
/// <typeparam name="T">Wrapped success type.</typeparam>
public readonly record struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    private readonly T? _value;
    private readonly string? _error;

    /// <summary>True when the operation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Success value. Throws when accessed on a failed result.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result.");

    /// <summary>Error message. Throws when accessed on a successful result.</summary>
    public string Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    /// <summary>Create a successful result.</summary>
    /// <param name="value">Success value.</param>
    /// <returns>Result wrapping <paramref name="value"/>.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>Create a failed result.</summary>
    /// <param name="error">Human-readable error message.</param>
    /// <returns>Failed result carrying <paramref name="error"/>.</returns>
    public static Result<T> Failure(string error) => new(false, default, error);
}
