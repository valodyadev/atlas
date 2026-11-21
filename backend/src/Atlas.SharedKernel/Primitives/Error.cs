namespace Atlas.SharedKernel.Primitives;

/// <summary>How a failure should be surfaced to a caller.</summary>
public enum ErrorKind
{
    /// <summary>Caller sent something invalid. Maps to HTTP 400.</summary>
    Validation,

    /// <summary>The thing does not exist. Maps to HTTP 404.</summary>
    NotFound,

    /// <summary>State does not permit the operation. Maps to HTTP 409.</summary>
    Conflict,

    /// <summary>Caller is not allowed. Maps to HTTP 403.</summary>
    Forbidden,

    /// <summary>Something broke on our side. Maps to HTTP 500.</summary>
    Unexpected,
}

/// <summary>
/// A machine-readable failure. The <see cref="Code"/> is part of the public API
/// contract and must stay stable; <see cref="Message"/> is for humans and may change.
/// </summary>
/// <param name="Code">Stable, dotted error code, e.g. <c>identity.user.email_taken</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Kind">How the failure maps onto a transport.</param>
public readonly record struct Error(string Code, string Message, ErrorKind Kind)
{
    /// <summary>Creates a validation error.</summary>
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    /// <summary>Creates a not-found error.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    /// <summary>Creates a conflict error.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    /// <summary>Creates a forbidden error.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    /// <summary>Creates an unexpected error.</summary>
    public static Error Unexpected(string code, string message) => new(code, message, ErrorKind.Unexpected);
}
