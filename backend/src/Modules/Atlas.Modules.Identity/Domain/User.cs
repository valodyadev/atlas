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
/// Invariants live here, not in the endpoint or the DbContext: the only way to
/// get a <see cref="User"/> is through <see cref="Register"/>, which cannot
/// produce an invalid one.
/// </remarks>
public sealed class User : AggregateRoot<Guid>
{
    private User() { }

    private User(Guid id, string email, string displayName, DateTimeOffset createdAt) : base(id)
    {
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    /// <summary>Normalised (lower-cased, trimmed) email address. Unique.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Name shown in the UI.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Maximum stored length of <see cref="Email"/>.</summary>
    public const int EmailMaxLength = 320;

    /// <summary>Maximum stored length of <see cref="DisplayName"/>.</summary>
    public const int DisplayNameMaxLength = 120;

    /// <summary>
    /// Creates a new user, or returns a validation error if the input cannot
    /// form a valid one.
    /// </summary>
    /// <param name="email">Email address; normalised before storage.</param>
    /// <param name="displayName">Name shown in the UI.</param>
    /// <param name="now">Creation instant, supplied by the caller's clock.</param>
    /// <returns>The new user, or an error.</returns>
    public static Result<User> Register(string? email, string? displayName, DateTimeOffset now)
    {
        var normalisedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
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

        var user = new User(Guid.CreateVersion7(), normalisedEmail, normalisedName, now);
        user.Raise(new UserRegistered(user.Id, user.Email, now));
        return user;
    }

    /// <summary>Changes the display name.</summary>
    /// <param name="displayName">The new name.</param>
    /// <returns>Success, or a validation error.</returns>
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
}
