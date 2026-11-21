using Atlas.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.DbMigrator;

/// <summary>
/// Applies each module's pending migrations, one module at a time.
/// </summary>
/// <remarks>
/// Register every module's DbContext here when the module is added. The list
/// is explicit rather than discovered so that the migration order is a
/// reviewable decision rather than an accident of assembly loading.
/// </remarks>
internal static class MigrationRunner
{
    private static readonly (string Module, Func<IServiceProvider, CancellationToken, Task> Apply)[] Migrations =
    [
        ("identity", static (services, cancellationToken) =>
            services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken)),
    ];

    /// <summary>Applies all pending migrations.</summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task RunAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var (module, apply) in Migrations)
        {
            logger.LogInformation("Applying migrations for module {Module}...", module);

            // A scope per module: a failure cannot leave another module's
            // context in a half-used state, and connection pooling behaves
            // the same as it does in the API.
            await using var scope = services.CreateAsyncScope();
            await apply(scope.ServiceProvider, cancellationToken);

            logger.LogInformation("Module {Module} is up to date.", module);
        }
    }
}
