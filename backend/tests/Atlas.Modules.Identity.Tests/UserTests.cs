using Atlas.Modules.Identity.Domain;
using Atlas.SharedKernel.Primitives;
using Shouldly;

namespace Atlas.Modules.Identity.Tests;

/// <summary>
/// Behaviour of the <see cref="User"/> aggregate. The factory method is the
/// only way to create one, so these tests cover every path into the type.
/// </summary>
public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A stand-in hash. The aggregate never hashes anything itself - it is
    /// handed an already-hashed password - so these tests do not need a real
    /// Argon2 run, which costs 19 MiB and milliseconds apiece.
    /// </summary>
    private const string TestHash = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2E=$aGFzaGhhc2g=";

    [Fact]
    public void Register_normalises_email_and_trims_name()
    {
        var result = User.Register("  Ada@Example.COM ", "  Ada Lovelace  ", TestHash, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("ada@example.com");
        result.Value.DisplayName.ShouldBe("Ada Lovelace");
        result.Value.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Register_assigns_a_version_7_identity()
    {
        // The primary key doubles as the keyset pagination cursor, which relies
        // on the UUIDv7 layout: a 48-bit big-endian Unix-millisecond prefix,
        // then the version nibble, then randomness.
        var id = User.Register("ada@example.com", "Ada", TestHash, Now).Value.Id;
        var bytes = id.ToByteArray(bigEndian: true);

        (bytes[6] >> 4).ShouldBe(7);

        long milliseconds = 0;
        for (var index = 0; index < 6; index++)
        {
            milliseconds = (milliseconds << 8) | bytes[index];
        }

        DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            .ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Ids_minted_in_later_milliseconds_sort_after_earlier_ones()
    {
        // Monotonicity holds ACROSS milliseconds, not within one: inside a
        // single millisecond the tail is random, so two users created in the
        // same millisecond have an arbitrary (but stable) relative order.
        // Keyset pagination only needs a stable total order, so that is fine -
        // but it does mean same-millisecond rows are not guaranteed to come
        // back in creation order.
        var earlier = User.Register("a@example.com", "A", TestHash, Now).Value.Id.ToString();
        await Task.Delay(5, TestContext.Current.CancellationToken);
        var later = User.Register("b@example.com", "B", TestHash, Now).Value.Id.ToString();

        string.CompareOrdinal(later, earlier).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Register_raises_UserRegistered()
    {
        var user = User.Register("ada@example.com", "Ada", TestHash, Now).Value;

        var raised = user.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<UserRegistered>();
        raised.UserId.ShouldBe(user.Id);
        raised.Email.ShouldBe("ada@example.com");
        raised.OccurredAt.ShouldBe(Now);
        raised.EventId.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    public void Register_rejects_invalid_email(string? email)
    {
        var result = User.Register(email, "Ada", TestHash, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("identity.user.email_invalid");
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
    }

    [Fact]
    public void Register_rejects_email_over_the_stored_length()
    {
        // The database column is bounded, so the domain must reject anything
        // that would be silently truncated or blow up on insert.
        var tooLong = new string('a', User.EmailMaxLength) + "@example.com";

        var result = User.Register(tooLong, "Ada", TestHash, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("identity.user.email_invalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_blank_display_name(string? displayName)
    {
        var result = User.Register("ada@example.com", displayName, TestHash, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("identity.user.display_name_invalid");
    }

    [Fact]
    public void Rename_updates_the_display_name()
    {
        var user = User.Register("ada@example.com", "Ada", TestHash, Now).Value;

        var result = user.Rename("  Ada King  ");

        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Ada King");
    }

    [Fact]
    public void Rename_rejects_a_blank_name_and_leaves_the_aggregate_untouched()
    {
        var user = User.Register("ada@example.com", "Ada", TestHash, Now).Value;

        var result = user.Rename("   ");

        result.IsFailure.ShouldBeTrue();
        user.DisplayName.ShouldBe("Ada");
    }

    [Fact]
    public void ClearDomainEvents_empties_the_queue()
    {
        var user = User.Register("ada@example.com", "Ada", TestHash, Now).Value;

        user.ClearDomainEvents();

        user.DomainEvents.ShouldBeEmpty();
    }
}
