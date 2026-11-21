using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Options;

/// <summary>
/// Bearer-token authentication settings.
/// </summary>
/// <remarks>
/// Disabled by default so the starter runs with no identity provider. Turn it
/// on per environment; <c>Development</c> is the only environment allowed to
/// have it off, and startup fails loudly otherwise.
/// </remarks>
public sealed class AuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>Whether bearer-token authentication is enforced.</summary>
    public bool Enabled { get; init; }

    /// <summary>OIDC issuer, e.g. <c>https://login.example.com/</c>.</summary>
    public string? Authority { get; init; }

    /// <summary>Expected <c>aud</c> claim.</summary>
    public string? Audience { get; init; }

    /// <summary>Whether HTTPS metadata retrieval is required. Only relax locally.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;
}

/// <summary>Global request-rate limiting.</summary>
public sealed class RateLimitOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimit";

    /// <summary>Requests permitted per window, per client.</summary>
    [Range(1, 1_000_000)]
    public int PermitLimit { get; init; } = 300;

    /// <summary>Window length in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Requests allowed to wait instead of being rejected.</summary>
    [Range(0, 10_000)]
    public int QueueLimit { get; init; }
}
