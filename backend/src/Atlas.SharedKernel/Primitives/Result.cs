namespace Atlas.SharedKernel.Primitives;

/// <summary>
/// Outcome of an operation that is expected to fail in known ways.
/// Exceptions stay reserved for genuinely unexpected conditions, which keeps
/// the happy path readable and the failure path exhaustive.
/// </summary>
public readonly record struct Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        ErrorOrNull = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure, or <see langword="null"/> on success.</summary>
    public Error? ErrorOrNull { get; }

    /// <summary>The failure. Throws when the result is a success.</summary>
    public Error Error => ErrorOrNull
        ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>A successful result.</summary>
    public static Result Success() => new(true, null);

    /// <summary>A failed result.</summary>
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>
/// Outcome of an operation that returns a value when it succeeds.
/// </summary>
/// <typeparam name="TValue">The success value type.</typeparam>
public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        ErrorOrNull = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure, or <see langword="null"/> on success.</summary>
    public Error? ErrorOrNull { get; }

    /// <summary>The failure. Throws when the result is a success.</summary>
    public Error Error => ErrorOrNull
        ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>The value. Throws when the result is a failure.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value ({ErrorOrNull?.Code}).");

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static Result<TValue> Success(TValue value) => new(true, value, null);

    /// <summary>A failed result.</summary>
    public static Result<TValue> Failure(Error error) => new(false, default, error);

    /// <summary>Implicitly lifts a value into a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly lifts an error into a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    /// <summary>Collapses both branches into a single value.</summary>
    /// <typeparam name="TOut">The collapsed type.</typeparam>
    /// <param name="onSuccess">Invoked when the result succeeded.</param>
    /// <param name="onFailure">Invoked when the result failed.</param>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }
}
