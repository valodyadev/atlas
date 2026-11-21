using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Atlas.ServiceDefaults.ErrorHandling;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> for an endpoint's request
/// body and short-circuits with a 400 problem response when it fails.
/// </summary>
/// <remarks>
/// Applied per endpoint rather than globally, so an endpoint without a
/// validator costs nothing and the validated contract is visible at the call
/// site: <c>.WithRequestValidation&lt;CreateUserRequest&gt;()</c>.
/// </remarks>
/// <typeparam name="TRequest">The request body type.</typeparam>
/// <param name="validator">Validator for <typeparamref name="TRequest"/>.</param>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Results.ValidationProblem(errors, title: "Validation");
    }
}

/// <summary>Attaches <see cref="ValidationFilter{TRequest}"/> to an endpoint.</summary>
public static class ValidationFilterExtensions
{
    /// <summary>Validates the endpoint's <typeparamref name="TRequest"/> body.</summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <param name="builder">The endpoint being built.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
    }
}
