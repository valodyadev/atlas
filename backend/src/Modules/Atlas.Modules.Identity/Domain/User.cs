using Atlas.SharedKernel.Domain;
using Atlas.SharedKernel.Primitives;

namespace Atlas.Modules.Identity.Domain;

/// <summary>Raised when a user account is first created.</summary>
/// <param name="UserId">The new user's identity.</param>
/// <param name="Email">The normalised email address.</param>
/// <param name="OccurredAt">When the account was created.</param>
public sealed record UserRegistered(Guid UserId, string Email, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);

/// <summary>
/// A person who can authenticate. Aggregate root of the Identity module.
/// </summary>
/// <remarks>
/// The aggregate never sees a plaintext password. Hashing happens in the
/// application layer and only the resulting hash reaches here, so a password
/// cannot be logged, serialised or persisted by accident.
/// </remarks>
public sealed class User : AggregateRoot<Guid>
{
    private User() { }

    private User(Guid id, string email, string displayName, string passwordHash, DateTimeOffset createdAt) : base(id)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        SecurityStamp = Guid.CreateVersion7();
    }

    /// <summary>Normalised (lower-cased, trimmed) email address. Unique.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Name shown in the UI.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Argon2id hash of the password, in PHC string format.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Changes whenever credentials change or every session is revoked.
    /// Embedded in access tokens so already-issued ones can be rejected
    /// without a database lookup on every request.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>Authorization role. One per user for now; see ADR 0010.</summary>
    public string Role { get; private set; } = Roles.Member;

    /// <summary>Consecutive failed sign-in attempts since the last success.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>When the account stops being locked, if it is.</summary>
    public DateTimeOffset? LockoutUntil { get; private set; }

    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Maximum stored length of <see cref="Email"/>.</summary>
    public const int EmailMaxLength = 320;

    /// <summary>Maximum stored length of <see cref="DisplayName"/>.</summary>
    public const int DisplayNameMaxLength = 120;

    /// <summary>Maximum stored length of <see cref="PasswordHash"/>.</summary>
    public const int PasswordHashMaxLength = 512;

    /// <summary>Normalises an email for storage and lookup.</summary>
    public static string NormaliseEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Creates a new user from an already-hashed password.
    /// </summary>
    /// <param name="email">Email address; normalised before storage.</param>
    /// <param name="displayName">Name shown in the UI.</param>
    /// <param name="passwordHash">Hash produced by the application layer.</param>
    /// <param name="now">Creation instant, supplied by the caller's clock.</param>
    public static Result<User> Register(string? email, string? displayName, string passwordHash, DateTimeOffset now)
    {
        var normalisedEmail = NormaliseEmail(email);
        var normalisedName = (displayName ?? string.Empty).Trim();

        if (normalisedEmail.Length is 0 or > EmailMaxLength || !normalisedEmail.Contains('@', StringComparison.Ordinal))
        {
            return Error.Validation("identity.user.email_invalid", "A valid email address is required.");
        }

        if (normalisedName.Length is 0 or > DisplayNameMaxLength)
        {
            return Error.Validation(
                "identity.user.display_name_invalid",
                $"Display name must be between 1 and {DisplayNameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Error.Unexpected("identity.user.password_hash_missing", "A password hash is required.");
        }

        var user = new User(Guid.CreateVersion7(), normalisedEmail, normalisedName, passwordHash, now);
        user.Raise(new UserRegistered(user.Id, user.Email, now));
        return user;
    }

    /// <summary>Changes the display name.</summary>
    public Result Rename(string? displayName)
    {
        var normalised = (displayName ?? string.Empty).Trim();
        if (normalised.Length is 0 or > DisplayNameMaxLength)
        {
            return Result.Failure(Error.Validation(
                "identity.user.display_name_invalid",
                $"Display name must be between 1 and {DisplayNameMaxLength} characters."));
        }

        DisplayName = normalised;
        return Result.Success();
    }

    /// <summary>Whether sign-in attempts are currently blocked.</summary>
    public bool IsLockedOut(DateTimeOffset now) => LockoutUntil is { } until && until > now;

    /// <summary>
    /// Records a failed sign-in and locks the account once the threshold is
    /// passed, backing off exponentially so repeated attacks become expensive
    /// while a genuine user mistyping twice is barely inconvenienced.
    /// </summary>
    /// <returns>The lockout end time if this attempt caused one.</returns>
    public DateTimeOffset? RecordFailedLogin(DateTimeOffset now, int threshold, TimeSpan baseLockout, TimeSpan maxLockout)
    {
        FailedLoginCount++;

        if (FailedLoginCount < threshold)
        {
            return null;
        }

        var overBy = FailedLoginCount - threshold;        // 0, 1, 2, ...
        var scaled = baseLockout * Math.Pow(2, Math.Min(overBy, 10));
        LockoutUntil = now.Add(scaled < maxLockout ? scaled : maxLockout);
        return LockoutUntil;
    }

    /// <summary>Clears the failure counter after a successful sign-in.</summary>
    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    /// <summary>
    /// Invalidates every access token already issued for this user, by
    /// rotating the stamp they were signed with.
    /// </summary>
    public void InvalidateIssuedTokens() => SecurityStamp = Guid.CreateVersion7();
}

/// <summary>
/// The roles the system understands.
/// </summary>
/// <remarks>
/// Deliberately a single role per user for now. Endpoints authorise against
/// <em>policies</em>, never against a role name directly, so moving to a
/// roles-and-permissions table later changes where the claim comes from
/// without touching a single endpoint. See ADR 0010.
/// </remarks>
public static class Roles
{
    /// <summary>An ordinary signed-in user.</summary>
    public const string Member = "member";

    /// <summary>Administrative access.</summary>
    public const string Administrator = "administrator";
}
