using Atlas.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// One PostgreSQL container shared by every integration test class, with the
/// real migrations applied once.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>
    /// Pinned image. Must match the version in infra/docker/compose.yaml, or
    /// tests stop telling you anything about production.
    /// </summary>
    public const string PostgresImage = "postgres:18-alpine";

    private PostgreSqlContainer? _container;

    /// <summary>Connection string for the container, or null when unavailable.</summary>
    public string? ConnectionString { get; private set; }

    /// <summary>True once a container is running and migrated.</summary>
    public bool IsAvailable => ConnectionString is not null;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (!ContainerRuntime.IsAvailable)
        {
            return;
        }

        _container = new PostgreSqlBuilder(PostgresImage)
            .WithDatabase("atlas")
            .WithUsername("atlas")
            .WithPassword("integration-tests")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply the same migrations the DbMigrator applies in production. A
        // broken migration therefore fails the test suite, not the deploy.
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(
                IdentityDbContext.MigrationsHistoryTable,
                IdentityDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new IdentityDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>Shares <see cref="PostgresFixture"/> across all integration tests.</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "integration";
}
