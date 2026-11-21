using Atlas.Modules.Identity.Application;
using Atlas.Modules.Identity.Contracts;
using Atlas.Modules.Identity.Endpoints;
using Atlas.Modules.Identity.Infrastructure;
using Atlas.ServiceDefaults.Diagnostics;
using Atlas.ServiceDefaults.Modularity;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.Modules.Identity;

/// <summary>
/// Composition root for the Identity module: everything the host needs to
/// know about this module is declared here and nowhere else.
/// </summary>
public sealed class IdentityModule : IApplicationModule
{
    /// <inheritdoc />
    public string Name => "identity";

    /// <inheritdoc />
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. "
                + "Copy .env.example to .env, or set the environment variable "
                + "ConnectionStrings__Default.");

        builder.Services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Keep each module's migration history inside its own schema so
                // modules can be migrated - and eventually split - independently.
                npgsql.MigrationsHistoryTable(
                    IdentityDbContext.MigrationsHistoryTable,
                    IdentityDbContext.Schema);
                npgsql.EnableRetryOnFailure();
            });

            // Postgres folds unquoted identifiers to lower case, so
            // PascalCase names end up permanently quoted. snake_case keeps
            // hand-written SQL, psql and migration diffs readable.
            options.UseSnakeCaseNamingConvention();

            // Expensive and leaky in production; invaluable locally.
            options.EnableDetailedErrors(builder.Environment.IsDevelopment());
            options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
        });

        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();

        builder.Services.AddHealthChecks()
            .AddCheck<IdentityDatabaseHealthCheck>(
                name: "identity-db",
                tags: [HealthExtensions.ReadyTag]);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        UserEndpoints.Map(endpoints);
    }
}
