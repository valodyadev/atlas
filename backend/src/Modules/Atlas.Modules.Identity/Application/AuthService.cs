using Atlas.Modules.Identity.Contracts;
using Atlas.Modules.Identity.Domain;
using Atlas.Modules.Identity.Infrastructure;
using Atlas.Modules.Identity.Options;
using Atlas.SharedKernel.Primitives;
using Atlas.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.Identity.Application;

/// <summary>
/// Registration, sign-in, token rotation and sign-out.
/// </summary>
/// <remarks>
/// Every failure path here returns the same error. That is deliberate: a
/// caller must not be able to tell a wrong password from an unknown account
/// from a locked one, because each distinction hands an attacker a way to
/// enumerate users. The log records which it was; the response never does.
/// </remarks>
/// <param name="dbContext">Module persistence.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="tokenService">Token minting.</param>
/// <param name="options">Credential policy.</param>
/// <param name="clock">Time source.</param>
/// <param name="logger">Logger.</param>
public sealed class AuthService(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IdentityAuthOptions options,
    IClock clock,
    ILogger<AuthService> logger)
{
    /// <summary>The single failure every credential path returns.</summary>
    private static readonly Error InvalidCredentials =
        Error.Validation("identity.auth.invalid_credentials", "Invalid email or password.");

    /// <summary>
    /// A real Argon2id hash of a throwaway password, verified against when no
    /// account exists so that a missing user costs the same wall-clock time as
    /// a wrong password. Without this, response timing enumerates accounts.
    /// </summary>
    private static readonly Lazy<string> DecoyHash =
        new(() => new Argon2PasswordHasher().Hash("decoy-password-for-constant-time-login"));

    /// <summary>
    /// Registers a user, and reports success whether or not the email was
    /// already taken.
    /// </summary>
    /// <remarks>
    /// Returning 409 on a duplicate would confirm which addresses have
    /// accounts. The cost is that we cannot tell a returning user "you already
    /// have an account" — the usual remedy is an email verification step,
    /// which this version does not have yet.
    /// </remarks>
    public async Task<Result<AcknowledgementResponse>> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = User.NormaliseEmail(request.Email);
        var now = clock.UtcNow;

        var alreadyExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Email == email, cancellationToken);

        if (alreadyExists)
        {
            // Hash anyway: skipping the expensive step here would make a
            // duplicate registration measurably faster than a new one.
            _ = passwordHasher.Hash(request.Password);
            logger.LogInformation("Registration attempted for an address that already exists.");
            return Acknowledgement();
        }

        var registration = User.Register(request.Email, request.DisplayName, passwordHasher.Hash(request.Password), now);
        if (registration.IsFailure)
        {
            return registration.Error;
        }

        dbContext.Users.Add(registration.Value);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent registration. The unique index
            // is the real guarantee; report the same acknowledgement.
            logger.LogInformation("Registration lost a race on a unique email.");
            return Acknowledgement();
        }

        registration.Value.ClearDomainEvents();
        logger.LogInformation("Registered user {UserId}.", registration.Value.Id);
        return Acknowledgement();
    }

    /// <summary>Signs a user in and starts a new refresh-token family.</summary>
    public async Task<Result<AuthTokensResponse>> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = User.NormaliseEmail(request.Email);
        var now = clock.UtcNow;

        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            // Spend the same time as a real verification would.
            _ = passwordHasher.Verify(request.Password, DecoyHash.Value);
            logger.LogInformation("Sign-in attempt for an unknown address.");
            return InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            logger.LogWarning("Sign-in attempt on locked account {UserId}.", user.Id);
            return InvalidCredentials;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var lockedUntil = user.RecordFailedLogin(
                now,
                options.LockoutThreshold,
                TimeSpan.FromSeconds(options.LockoutBaseSeconds),
                TimeSpan.FromSeconds(options.LockoutMaxSeconds));

            await dbContext.SaveChangesAsync(cancellationToken);

            if (lockedUntil is not null)
            {
                logger.LogWarning("Account {UserId} locked until {LockedUntil}.", user.Id, lockedUntil);
            }

            return InvalidCredentials;
        }

        user.RecordSuccessfulLogin();

        var (refreshToken, refreshHash) = tokenService.CreateRefreshToken();
        dbContext.RefreshTokens.Add(RefreshToken.StartFamily(
            user.Id, refreshHash, now, TimeSpan.FromDays(options.RefreshTokenDays)));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} signed in.", user.Id);
        return IssueTokens(user, refreshToken, now);
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair, rotating it.
    /// </summary>
    /// <remarks>
    /// If the presented token was already spent, that is either a replay or a
    /// stolen token. The safe assumption is theft, so the whole family is
    /// revoked and the legitimate user is signed out — better an unexpected
    /// sign-out than an attacker holding a live session.
    /// </remarks>
    public async Task<Result<AuthTokensResponse>> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var hash = tokenService.HashRefreshToken(refreshToken ?? string.Empty);

        var existing = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return InvalidCredentials;
        }

        if (existing.UsedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; family {FamilyId} revoked.",
                existing.UserId, existing.FamilyId);
            return InvalidCredentials;
        }

        if (!existing.IsActive(now))
        {
            return InvalidCredentials;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Id == existing.UserId, cancellationToken);

        if (user is null)
        {
            return InvalidCredentials;
        }

        var (newToken, newHash) = tokenService.CreateRefreshToken();
        var replacement = RefreshToken.Continue(
            user.Id, newHash, existing.FamilyId, now, TimeSpan.FromDays(options.RefreshTokenDays));

        dbContext.RefreshTokens.Add(replacement);
        existing.MarkUsed(now, replacement.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        return IssueTokens(user, newToken, now);
    }

    /// <summary>
    /// Signs out by revoking the presented token's whole family.
    /// </summary>
    /// <remarks>
    /// Always reports success. Reporting failure for an unknown token would
    /// tell a caller which tokens are real.
    /// </remarks>
    public async Task<Result<AcknowledgementResponse>> LogoutAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var hash = tokenService.HashRefreshToken(refreshToken ?? string.Empty);

        var existing = await dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (existing is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            logger.LogInformation("User {UserId} signed out.", existing.UserId);
        }

        return Acknowledgement();
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in family)
        {
            token.Revoke(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Result<AuthTokensResponse> IssueTokens(User user, string refreshToken, DateTimeOffset now)
    {
        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user, now);
        return new AuthTokensResponse(
            accessToken, refreshToken, (int)(expiresAt - now).TotalSeconds);
    }

    private static Result<AcknowledgementResponse> Acknowledgement() =>
        new AcknowledgementResponse("If that email can be registered, the account is ready. You can now sign in.");
}
