using System.Net;
using System.Net.Http.Json;
using Atlas.Modules.Identity.Contracts;
using Shouldly;

namespace Atlas.Api.IntegrationTests;

/// <summary>End-to-end behaviour of the user endpoints.</summary>
/// <param name="fixture">Shared database fixture.</param>
[Collection(IntegrationTestCollection.Name)]
public sealed class UserEndpointTests(PostgresFixture fixture)
{
    private AtlasApiFactory CreateFactory()
    {
        Assert.SkipUnless(fixture.IsAvailable, ContainerRuntime.SkipReason);
        return new AtlasApiFactory(fixture.ConnectionString!);
    }

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@example.com";

    [Fact]
    public async Task Create_then_fetch_round_trips_a_user()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var created = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(email, "Ada Lovelace"),
            TestContext.Current.CancellationToken);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location.ShouldNotBeNull();

        var body = await created.Content.ReadFromJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Email.ShouldBe(email);
        body.DisplayName.ShouldBe("Ada Lovelace");

        var fetched = await client.GetFromJsonAsync<UserResponse>(
            $"/api/v1/users/{body.Id}",
            TestContext.Current.CancellationToken);

        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(body.Id);
    }

    [Fact]
    public async Task Create_normalises_the_email_before_storing_it()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest(email.ToUpperInvariant(), "  Grace Hopper  "),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken);
        body!.Email.ShouldBe(email);
        body.DisplayName.ShouldBe("Grace Hopper");
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_with_409_and_a_stable_error_code()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();
        var request = new CreateUserRequest(email, "First");

        var first = await client.PostAsJsonAsync(
            "/api/v1/users", request, TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/v1/users", request, TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        problem.ShouldContain("identity.user.email_taken");
    }

    [Fact]
    public async Task Malformed_payload_is_rejected_with_400_problem_json()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserRequest("not-an-email", string.Empty),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Unknown_user_returns_404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/users/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_pages_with_a_cursor_and_never_repeats_a_row()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var index = 0; index < 5; index++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/users",
                new CreateUserRequest(UniqueEmail(), $"Paged {index}"),
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        var seen = new List<Guid>();
        string? cursor = null;

        // Walk the whole collection two rows at a time.
        for (var page = 0; page < 50; page++)
        {
            var url = cursor is null ? "/api/v1/users?limit=2" : $"/api/v1/users?limit=2&cursor={cursor}";
            var result = await client.GetFromJsonAsync<Page<UserResponse>>(
                url, TestContext.Current.CancellationToken);

            result.ShouldNotBeNull();
            seen.AddRange(result.Items.Select(user => user.Id));

            cursor = result.NextCursor;
            if (cursor is null)
            {
                break;
            }
        }

        cursor.ShouldBeNull("paging should terminate");
        seen.Count.ShouldBeGreaterThanOrEqualTo(5);
        seen.Distinct().Count().ShouldBe(seen.Count, "keyset paging must not repeat rows");
    }

    /// <summary>Mirrors <c>Atlas.SharedKernel.Primitives.Page&lt;T&gt;</c> for deserialisation.</summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="Items">Page items.</param>
    /// <param name="NextCursor">Cursor for the next page.</param>
    private sealed record Page<TItem>(IReadOnlyList<TItem> Items, string? NextCursor);
}
