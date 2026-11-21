using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// Boots the real API host in-process, pointed at the test database.
/// </summary>
/// <remarks>
/// Configuration is overridden rather than code: the host builds its own
/// services exactly as it does in production, so middleware order, options
/// binding and endpoint wiring are all genuinely exercised.
/// </remarks>
/// <param name="connectionString">Connection string for the test database.</param>
public sealed class AtlasApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Test-only signing key. Never used outside the test host.</summary>
    public const string SigningKey = "integration-test-signing-key-at-least-32-bytes-long";

    /// <summary>Issuer the test host expects.</summary>
    public const string Issuer = "atlas-tests";

    /// <summary>Audience the test host expects.</summary>
    public const string Audience = "atlas-api-tests";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);

        // Auth is ON in tests. Running the suite with authentication disabled
        // would mean the authorization rules were never exercised, which is
        // precisely the coverage that matters here.
        builder.UseSetting("Auth:Enabled", "true");
        builder.UseSetting("Identity:Auth:SigningKey", SigningKey);
        builder.UseSetting("Identity:Auth:Issuer", Issuer);
        builder.UseSetting("Identity:Auth:Audience", Audience);

        // Keep Argon2 honest but the suite quick: the cost parameters are
        // fixed in code, so what we shorten here is the lockout window.
        builder.UseSetting("Identity:Auth:LockoutThreshold", "3");
        builder.UseSetting("Identity:Auth:LockoutBaseSeconds", "1");

        // The rate limiter is exercised by its own test; everything else would
        // otherwise start failing once the suite grows past the window.
        builder.UseSetting("RateLimit:PermitLimit", "100000");
    }
}
