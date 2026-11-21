using Atlas.SharedKernel.Primitives;
using Shouldly;

namespace Atlas.Modules.Identity.Tests;

/// <summary>
/// The result type is used by every use case in every module, so its edges are
/// worth nailing down once.
/// </summary>
public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_the_value_and_no_error()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.ErrorOrNull.ShouldBeNull();
    }

    [Fact]
    public void Failure_exposes_the_error_and_throws_on_value()
    {
        Result<int> result = Error.NotFound("thing.missing", "No such thing.");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("thing.missing");
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Match_collapses_both_branches()
    {
        Result<int> ok = 7;
        Result<int> bad = Error.Conflict("busy", "Try later.");

        ok.Match(value => $"ok:{value}", error => $"err:{error.Code}").ShouldBe("ok:7");
        bad.Match(value => $"ok:{value}", error => $"err:{error.Code}").ShouldBe("err:busy");
    }

    [Fact]
    public void PageRequest_clamps_the_limit()
    {
        PageRequest.Of(null, null).Limit.ShouldBe(PageRequest.DefaultLimit);
        PageRequest.Of(0, null).Limit.ShouldBe(1);
        PageRequest.Of(-5, null).Limit.ShouldBe(1);
        PageRequest.Of(10_000, null).Limit.ShouldBe(PageRequest.MaxLimit);
        PageRequest.Of(25, "cursor").ShouldBe(new PageRequest(25, "cursor"));
    }
}
