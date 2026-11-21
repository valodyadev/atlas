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
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Auth:Enabled", "false");

        // The rate limiter is exercised by its own test; everything else would
        // otherwise start failing once the suite grows past the window.
        builder.UseSetting("RateLimit:PermitLimit", "100000");
    }
}
