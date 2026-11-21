using Atlas.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.Identity.Infrastructure;

/// <summary>
/// Persistence for the Identity module.
/// </summary>
/// <remarks>
/// One DbContext and one Postgres schema per module. Each module owns its
/// tables outright: no other module may read them directly and no foreign key
/// crosses a schema boundary. That restriction is what makes it possible to
/// move this schema to its own database later without rewriting queries.
/// </remarks>
/// <param name="options">EF Core options supplied by DI.</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    /// <summary>The schema owned by this module.</summary>
    public const string Schema = "identity";

    /// <summary>Name of the EF migrations history table, kept inside the module schema.</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Registered users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Default every string to a bounded length so a forgotten MaxLength
        // never silently becomes an unbounded text column.
        configurationBuilder.Properties<string>().HaveMaxLength(256);

        base.ConfigureConventions(configurationBuilder);
    }
}
