using Atlas.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Api.Extensions;

/// <summary>Bearer-token authentication and secure-by-default authorization.</summary>
internal static class AuthenticationExtensions
{
    internal static IHostApplicationBuilder AddApiAuthentication(this IHostApplicationBuilder builder)
    {
        var authOptions = builder.Configuration
            .GetSection(AuthOptions.SectionName)
            .Get<AuthOptions>() ?? new AuthOptions();

        builder.Services.AddSingleton(authOptions);

        // Shipping with auth switched off outside Development is the kind of
        // mistake that is only ever found in production. Fail at startup.
        if (!authOptions.Enabled && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Auth:Enabled must be true in the '{builder.Environment.EnvironmentName}' environment.");
        }

        if (!authOptions.Enabled)
        {
            // Keep the middleware shape identical between environments so that
            // "works locally, 401s in staging" cannot be a configuration-shape
            // problem. Nothing is enforced while disabled.
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();
            return builder;
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authOptions.Authority;
                options.Audience = authOptions.Audience;
                options.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // Every endpoint requires a valid token unless it opts out with
        // AllowAnonymous(). Forgetting to protect a new endpoint is therefore
        // not possible; forgetting to open one up is an obvious 401.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return builder;
    }
}
