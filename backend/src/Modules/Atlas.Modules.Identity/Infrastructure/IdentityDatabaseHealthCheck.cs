using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Atlas.Modules.Identity.Infrastructure;

/// <summary>
/// Readiness check for the Identity module's database.
/// </summary>
/// <remarks>
/// Each module publishes its own readiness check, tagged <c>ready</c>, so a
/// degraded dependency is attributable to a module instead of surfacing as a
/// single opaque "unhealthy" for the whole host.
/// </remarks>
/// <param name="dbContext">Module persistence.</param>
public sealed class IdentityDatabaseHealthCheck(IdentityDbContext dbContext) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Identity database is not reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Identity database check failed.", exception);
        }
    }
}
