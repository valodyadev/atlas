using Atlas.ServiceDefaults.Diagnostics;
using Atlas.ServiceDefaults.Observability;
using Atlas.SharedKernel.Time;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Atlas.ServiceDefaults;

/// <summary>
/// The one call every .NET entry point in this repo makes.
/// </summary>
/// <remarks>
/// Centralising host defaults here is what stops the fifth service from being
/// configured subtly differently from the first. If a default needs to change
/// fleet-wide, it changes in exactly one place.
/// </remarks>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Adds telemetry, health checks, resilient HTTP defaults, problem details
    /// and a testable clock.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddObservability();
        builder.AddDefaultHealthChecks();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<IClock, SystemClock>();

        // Every HttpClient in the process gets retries, a circuit breaker and
        // timeouts by default. Opting out has to be deliberate.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
        });

        // RFC 9457 problem+json for unhandled failures, with the trace id
        // attached so a user-visible error can be found in the trace backend.
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance =
                    $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                context.ProblemDetails.Extensions["traceId"] =
                    System.Diagnostics.Activity.Current?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;
            };
        });

        return builder;
    }

    /// <summary>Maps the endpoints every service exposes.</summary>
    /// <param name="endpoints">Route builder to map onto.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHealthEndpoints();
        return endpoints;
    }
}
