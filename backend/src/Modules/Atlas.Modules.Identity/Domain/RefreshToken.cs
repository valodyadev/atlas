using Atlas.SharedKernel.Domain;

namespace Atlas.Modules.Identity.Domain;

/// <summary>
/// A rotating refresh token.
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the token is stored. The plaintext exists once, in
/// the response to the client, and is never persisted or logged — a database
/// leak must not hand an attacker usable sessions.
///
/// Tokens form a <em>family</em>: each rotation links the new token to the same
/// <see cref="FamilyId"/>. Presenting a token that has already been used means
/// either a replay or a stolen token, and the correct response is to revoke the
/// whole family rather than just that one token. That is the entire reason for
/// rotating rather than issuing one long-lived token.
/// </remarks>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken() { }

    private RefreshToken(Guid id, Guid userId, string tokenHash, Guid familyId,
                         DateTimeOffset issuedAt, DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Owner of this token.</summary>
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the token, hex encoded. Never the token itself.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Groups every token descended from one login.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>When this token was issued.</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>When this token stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When this token was exchanged. Non-null means already spent.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>When this token was explicitly revoked.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The token issued in exchange for this one.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>Maximum stored length of a hex-encoded SHA-256 hash.</summary>
    public const int TokenHashLength = 64;

    /// <summary>Starts a new family. Used at login.</summary>
    public static RefreshToken StartFamily(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), userId, tokenHash, Guid.CreateVersion7(), now, now.Add(lifetime));

    /// <summary>Continues an existing family. Used on rotation.</summary>
    public static RefreshToken Continue(Guid userId, string tokenHash, Guid familyId,
                                        DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), userId, tokenHash, familyId, now, now.Add(lifetime));

    /// <summary>Whether this token can still be exchanged.</summary>
    public bool IsActive(DateTimeOffset now) =>
        UsedAt is null && RevokedAt is null && ExpiresAt > now;

    /// <summary>Marks this token spent and records its successor.</summary>
    public void MarkUsed(DateTimeOffset now, Guid replacementId)
    {
        UsedAt = now;
        ReplacedByTokenId = replacementId;
    }

    /// <summary>Revokes this token. Idempotent.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
