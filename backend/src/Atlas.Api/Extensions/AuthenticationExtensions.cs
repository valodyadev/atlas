using Atlas.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Atlas.Api.Extensions;

/// <summary>Bearer-token authentication and secure-by-default authorization.</summary>
/// <remarks>
/// The Identity module issues the tokens; this host validates them. Both read
/// the same <c>Identity:Auth</c> configuration section, so the signing key,
/// issuer and audience cannot drift apart between minting and checking.
/// </remarks>
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

        var signingKey = builder.Configuration["Identity:Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Identity:Auth:SigningKey is required to validate access tokens. "
                + "Generate one with: openssl rand -base64 48");
        }

        var issuer = builder.Configuration["Identity:Auth:Issuer"] ?? "atlas";
        var audience = builder.Configuration["Identity:Auth:Audience"] ?? "atlas-api";

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    // Tight, because this system issues and validates its own
                    // tokens on one clock. Generous skew just extends the life
                    // of an expired token.
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
