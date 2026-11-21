using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Atlas.ServiceDefaults.Diagnostics;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// The split matters in an orchestrator: a failing <em>readiness</em> probe
/// takes the instance out of the load balancer, while a failing
/// <em>liveness</em> probe gets it killed and restarted. Dependency checks
/// (database, cache, queue) therefore belong to readiness only - otherwise a
/// brief database blip restarts the whole fleet.
/// </remarks>
public static class HealthExtensions
{
    /// <summary>Tag applied to checks that gate readiness.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Registers the health check pipeline.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        return builder;
    }

    /// <summary>Maps <c>/health/live</c> and <c>/health/ready</c>.</summary>
    /// <param name="endpoints">Route builder to map onto.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Liveness: is the process itself healthy? Never touches dependencies.
        endpoints.MapHealthChecks("/health/live", new()
        {
            Predicate = registration => registration.Tags.Contains("live"),
        }).AllowAnonymous();

        // Readiness: can this instance serve traffic right now?
        endpoints.MapHealthChecks("/health/ready", new()
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        }).AllowAnonymous();

        return endpoints;
    }
}
