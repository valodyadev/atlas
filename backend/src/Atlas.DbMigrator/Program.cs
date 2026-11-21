using Atlas.DbMigrator;
using Atlas.Modules.Identity;
using Atlas.ServiceDefaults;
using Atlas.ServiceDefaults.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// Atlas database migrator.
//
// Migrations are applied by this job, not by the API at startup. Startup
// migration looks convenient and then breaks the first time two API replicas
// roll at once, or the first time a migration needs to be reviewed, timed or
// rolled back independently of a deploy. In CI/CD this runs as its own step
// (a Kubernetes Job, an ECS task, a pipeline stage) before the new API
// version is released.
//
// Exit codes:  0 = up to date   1 = failed
// ---------------------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "ATLAS_");

// Same baseline as the API. Modules are entitled to assume these services
// exist, and a migration is exactly the kind of deploy step whose traces and
// duration you want in the same telemetry backend as everything else.
builder.AddServiceDefaults();

// Reusing the module's own ConfigureServices means the migrator can never
// disagree with the API about how a DbContext is configured.
builder.AddModules(new IdentityModule());

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Atlas.DbMigrator");

// Give the job a hard ceiling so a stuck lock fails the deploy instead of
// hanging the pipeline indefinitely.
var timeout = TimeSpan.FromMinutes(
    int.TryParse(builder.Configuration["Migrations:TimeoutMinutes"], out var minutes) ? minutes : 10);

using var cancellation = new CancellationTokenSource(timeout);

try
{
    await MigrationRunner.RunAsync(host.Services, logger, cancellation.Token);
    logger.LogInformation("All migrations applied.");
    return 0;
}
catch (OperationCanceledException)
{
    logger.LogCritical("Migration timed out after {Timeout}.", timeout);
    return 1;
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Migration failed.");
    return 1;
}
