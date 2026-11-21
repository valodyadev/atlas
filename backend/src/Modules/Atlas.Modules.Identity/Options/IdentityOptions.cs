using System.ComponentModel.DataAnnotations;

namespace Atlas.Modules.Identity.Options;

/// <summary>
/// Everything about how this module issues and ages credentials.
/// </summary>
/// <remarks>
/// The signing key is a secret and never has a default. It comes from the
/// environment or a secret manager; see docs/security/secrets.md.
/// </remarks>
public sealed class IdentityAuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Auth";

    /// <summary>Symmetric signing key for access tokens. At least 32 bytes.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Token issuer, e.g. <c>https://api.example.com</c>.</summary>
    public string Issuer { get; init; } = "atlas";

    /// <summary>Intended audience of issued tokens.</summary>
    public string Audience { get; init; } = "atlas-api";

    /// <summary>
    /// Access token lifetime. Short on purpose: an access token cannot be
    /// revoked, so its blast radius is bounded only by how fast it expires.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Refresh token lifetime, renewed on each rotation.</summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;

    /// <summary>Consecutive failures before the account locks.</summary>
    [Range(1, 100)]
    public int LockoutThreshold { get; init; } = 5;

    /// <summary>First lockout duration; doubles with each further failure.</summary>
    [Range(1, 3600)]
    public int LockoutBaseSeconds { get; init; } = 30;

    /// <summary>Ceiling on the exponential backoff.</summary>
    [Range(1, 86400)]
    public int LockoutMaxSeconds { get; init; } = 3600;

    /// <summary>Minimum password length. Length beats composition rules.</summary>
    [Range(8, 256)]
    public int MinimumPasswordLength { get; init; } = 12;
}
