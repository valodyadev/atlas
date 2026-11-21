using Atlas.SharedKernel.Primitives;
using Microsoft.AspNetCore.Http;

namespace Atlas.ServiceDefaults.ErrorHandling;

/// <summary>
/// Translates domain failures into RFC 9457 <c>application/problem+json</c>
/// responses, so every endpoint in every module reports errors identically and
/// clients can write one error handler.
/// </summary>
public static class ErrorResults
{
    /// <summary>Maps an <see cref="Error"/> onto an HTTP problem response.</summary>
    /// <param name="error">The domain failure.</param>
    /// <returns>A problem-details result with the matching status code.</returns>
    public static IResult ToProblem(this Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Kind.ToString(),
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    /// <summary>
    /// Collapses a <see cref="Result{TValue}"/> into an HTTP response.
    /// </summary>
    /// <typeparam name="TValue">The success payload type.</typeparam>
    /// <param name="result">The operation result.</param>
    /// <param name="onSuccess">Builds the success response.</param>
    /// <returns>The success response, or a problem response on failure.</returns>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.Match(onSuccess, error => error.ToProblem());
    }
}
