using System.Security.Cryptography;
using System.Text;
using Atlas.Modules.Identity.Domain;
using Atlas.Modules.Identity.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Modules.Identity.Application;

/// <summary>Claim names this module issues and the API authorises on.</summary>
public static class AtlasClaims
{
    /// <summary>Rotates when credentials change, invalidating issued tokens.</summary>
    public const string SecurityStamp = "sst";

    /// <summary>The user's authorization role.</summary>
    public const string Role = "role";
}

/// <summary>Mints access and refresh tokens.</summary>
public interface ITokenService
{
    /// <summary>Creates a signed, short-lived access token.</summary>
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user, DateTimeOffset now);

    /// <summary>Creates a refresh token: the secret, and the hash to store.</summary>
    (string Token, string Hash) CreateRefreshToken();

    /// <summary>Hashes a refresh token for lookup.</summary>
    string HashRefreshToken(string token);
}

/// <summary>
/// JWT access tokens plus opaque refresh tokens.
/// </summary>
/// <remarks>
/// Access tokens are JWTs so the API can authorise without a database round
/// trip. Refresh tokens are <em>not</em> JWTs: they are opaque random bytes,
/// because their whole purpose is to be revocable, and a self-describing
/// signed token is exactly the thing you cannot revoke.
///
/// Signing is HS256 while one service both issues and validates. The moment a
/// second service needs to validate without holding the signing secret, this
/// becomes asymmetric — see ADR 0011.
/// </remarks>
public sealed class TokenService : ITokenService
{
    private const int RefreshTokenBytes = 32;

    private readonly IdentityAuthOptions _options;
    private readonly SigningCredentials _credentials;

    public TokenService(IdentityAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        var keyBytes = Encoding.UTF8.GetBytes(options.SigningKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Identity:Auth:SigningKey must be at least 32 bytes. "
                + "Generate one with: openssl rand -base64 48");
        }

        _credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Name] = user.DisplayName,
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [AtlasClaims.Role] = user.Role,
                [AtlasClaims.SecurityStamp] = user.SecurityStamp.ToString(),
            },
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    /// <inheritdoc />
    public (string Token, string Hash) CreateRefreshToken()
    {
        // 256 bits of entropy. Base64url so it survives headers and JSON
        // without escaping.
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenBytes);
        var token = Base64UrlEncoder.Encode(bytes);
        return (token, HashRefreshToken(token));
    }

    /// <inheritdoc />
    public string HashRefreshToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        // SHA-256, not Argon2: the token already has 256 bits of entropy, so
        // there is nothing to brute force and a slow hash would only add
        // latency to every refresh.
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
