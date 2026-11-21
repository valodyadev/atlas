using Atlas.Modules.Identity.Application;
using Atlas.Modules.Identity.Contracts;
using Atlas.Modules.Identity.Endpoints;
using Atlas.Modules.Identity.Options;
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

        // Credential policy. The signing key is a secret with no default: the
        // module refuses to start without one rather than silently signing
        // tokens with something guessable.
        var authOptions = builder.Configuration
            .GetSection(IdentityAuthOptions.SectionName)
            .Get<IdentityAuthOptions>() ?? new IdentityAuthOptions();

        // Deliberately NOT validated here. Every host that loads this module
        // would then need a signing key, including the migration job, which
        // never issues a token — handing a secret to a process that has no use
        // for it. Validation happens where it matters instead:
        //
        //   * the API host, at startup, in AddApiAuthentication;
        //   * TokenService's constructor, if anything tries to mint a token.
        //
        // So the API still fails fast, and the migrator needs no secret.

        builder.Services.AddSingleton(authOptions);
        builder.Services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        builder.Services.AddSingleton<ITokenService, TokenService>();

        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<AuthService>();

        builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        builder.Services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();

        builder.Services.AddHealthChecks()
            .AddCheck<IdentityDatabaseHealthCheck>(
                name: "identity-db",
                tags: [HealthExtensions.ReadyTag]);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        AuthEndpoints.Map(endpoints);
        UserEndpoints.Map(endpoints);
    }
}
