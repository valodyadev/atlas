using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Atlas.Modules.Identity.Infrastructure;

/// <summary>
/// Lets <c>dotnet ef</c> construct the context without starting a host.
/// </summary>
/// <remarks>
/// With an explicit factory, migrations are generated from the module project
/// alone - no startup project, no application configuration, no database
/// connection required. That keeps schema ownership inside the module and
/// makes <c>dotnet ef</c> behave identically on a laptop and in CI.
/// </remarks>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    /// <inheritdoc />
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // Generating a migration never connects, so a syntactically valid
        // placeholder is enough. Applying migrations is the DbMigrator's job
        // and uses the real configured connection string.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=atlas;Username=atlas;Password=design-time";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                IdentityDbContext.MigrationsHistoryTable,
                IdentityDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IdentityDbContext(options);
    }
}
