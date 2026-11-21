using Atlas.Modules.Identity.Domain;
using Shouldly;

namespace Atlas.Modules.Identity.Tests;

/// <summary>Lockout and token-invalidation behaviour on the aggregate.</summary>
public sealed class AuthDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string TestHash = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdHNhbHQ=$aGFzaGhhc2g=";

    private static User NewUser() => User.Register("ada@example.com", "Ada", TestHash, Now).Value;

    [Fact]
    public void A_new_user_is_a_member_and_not_locked_out()
    {
        var user = NewUser();

        user.Role.ShouldBe(Roles.Member);
        user.FailedLoginCount.ShouldBe(0);
        user.IsLockedOut(Now).ShouldBeFalse();
        user.SecurityStamp.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Failures_below_the_threshold_do_not_lock_the_account()
    {
        var user = NewUser();

        for (var attempt = 1; attempt < 5; attempt++)
        {
            user.RecordFailedLogin(Now, threshold: 5, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1))
                .ShouldBeNull();
        }

        user.IsLockedOut(Now).ShouldBeFalse();
        user.FailedLoginCount.ShouldBe(4);
    }

    [Fact]
    public void The_threshold_failure_locks_the_account()
    {
        var user = NewUser();
        DateTimeOffset? locked = null;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            locked = user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));
        }

        locked.ShouldBe(Now.AddSeconds(30));
        user.IsLockedOut(Now).ShouldBeTrue();
        user.IsLockedOut(Now.AddSeconds(31)).ShouldBeFalse();
    }

    [Fact]
    public void Lockout_backs_off_exponentially_and_is_capped()
    {
        var user = NewUser();
        var max = TimeSpan.FromMinutes(2);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), max);
        }
        user.LockoutUntil.ShouldBe(Now.AddSeconds(30));      // 30 * 2^0

        user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), max);
        user.LockoutUntil.ShouldBe(Now.AddSeconds(60));      // 30 * 2^1

        // 30 * 2^2 = 120s, exactly the cap, then it stays there.
        user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), max);
        user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), max);
        (user.LockoutUntil!.Value - Now).ShouldBeLessThanOrEqualTo(max);
    }

    [Fact]
    public void A_successful_sign_in_clears_the_lockout()
    {
        var user = NewUser();
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            user.RecordFailedLogin(Now, 5, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));
        }
        user.IsLockedOut(Now).ShouldBeTrue();

        user.RecordSuccessfulLogin();

        user.IsLockedOut(Now).ShouldBeFalse();
        user.FailedLoginCount.ShouldBe(0);
        user.LockoutUntil.ShouldBeNull();
    }

    [Fact]
    public void Invalidating_tokens_rotates_the_security_stamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.InvalidateIssuedTokens();

        user.SecurityStamp.ShouldNotBe(before);
    }

    [Fact]
    public void Registration_requires_a_password_hash()
    {
        var result = User.Register("ada@example.com", "Ada", "", Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("identity.user.password_hash_missing");
    }
}

/// <summary>Rotation and revocation behaviour of refresh tokens.</summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    [Fact]
    public void A_fresh_token_is_active()
    {
        var token = RefreshToken.StartFamily(Guid.CreateVersion7(), "hash", Now, Lifetime);

        token.IsActive(Now).ShouldBeTrue();
        token.FamilyId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void An_expired_token_is_not_active()
    {
        var token = RefreshToken.StartFamily(Guid.CreateVersion7(), "hash", Now, Lifetime);

        token.IsActive(Now.Add(Lifetime).AddSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void A_used_token_is_not_active_and_points_at_its_successor()
    {
        var token = RefreshToken.StartFamily(Guid.CreateVersion7(), "hash", Now, Lifetime);
        var successor = Guid.CreateVersion7();

        token.MarkUsed(Now, successor);

        token.IsActive(Now).ShouldBeFalse();
        token.UsedAt.ShouldBe(Now);
        token.ReplacedByTokenId.ShouldBe(successor);
    }

    [Fact]
    public void Rotation_keeps_the_family()
    {
        var userId = Guid.CreateVersion7();
        var first = RefreshToken.StartFamily(userId, "hash-1", Now, Lifetime);

        var second = RefreshToken.Continue(userId, "hash-2", first.FamilyId, Now, Lifetime);

        second.FamilyId.ShouldBe(first.FamilyId);
        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public void Revocation_is_idempotent()
    {
        var token = RefreshToken.StartFamily(Guid.CreateVersion7(), "hash", Now, Lifetime);

        token.Revoke(Now);
        token.Revoke(Now.AddHours(1));

        token.RevokedAt.ShouldBe(Now);
        token.IsActive(Now).ShouldBeFalse();
    }
}
