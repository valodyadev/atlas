using Atlas.Modules.Identity.Application;
using Shouldly;

namespace Atlas.Modules.Identity.Tests;

/// <summary>
/// Argon2id hashing. These are genuinely slow by design — each call burns
/// 19 MiB and a few milliseconds, which is the whole point — so the suite
/// stays small and deliberate.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_the_password_it_hashed()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("correct horse battery staple", hash).ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_wrong_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("Correct horse battery staple", hash).ShouldBeFalse();
        _hasher.Verify("", hash).ShouldBeFalse();
    }

    [Fact]
    public void Salts_every_hash_separately()
    {
        // Identical passwords must not produce identical hashes, or a stolen
        // table reveals which users share a password.
        var first = _hasher.Hash("same password");
        var second = _hasher.Hash("same password");

        first.ShouldNotBe(second);
        _hasher.Verify("same password", first).ShouldBeTrue();
        _hasher.Verify("same password", second).ShouldBeTrue();
    }

    [Fact]
    public void Records_its_parameters_so_the_cost_can_be_raised_later()
    {
        var hash = _hasher.Hash("whatever");

        hash.ShouldStartWith("$argon2id$v=19$m=19456,t=2,p=1$");
        hash.Split('$', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$onlyfourparts")]
    [InlineData("$bcrypt$v=19$m=1,t=1,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=x,t=2,p=1$c2FsdA==$aGFzaA==")]
    public void A_malformed_stored_hash_fails_closed(string stored)
    {
        // It must return false, not throw. A corrupt row that throws becomes a
        // 500, which distinguishes it from a wrong password and leaks that the
        // account exists.
        _hasher.Verify("any password", stored).ShouldBeFalse();
    }
}
