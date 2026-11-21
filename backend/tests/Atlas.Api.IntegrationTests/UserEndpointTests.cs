using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atlas.Modules.Identity.Contracts;
using Shouldly;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// The user endpoints, which now require a signed-in caller. Account creation
/// lives at <c>/auth/register</c>; these only read.
/// </summary>
/// <param name="fixture">Shared database fixture.</param>
[Collection(IntegrationTestCollection.Name)]
public sealed class UserEndpointTests(PostgresFixture fixture)
{
    private const string Password = "correct horse battery staple";

    private AtlasApiFactory CreateFactory()
    {
        Assert.SkipUnless(fixture.IsAvailable, ContainerRuntime.SkipReason);
        return new AtlasApiFactory(fixture.ConnectionString!);
    }

    private static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@example.com";

    private sealed record Tokens(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType);

    /// <summary>Registers an account and returns a client holding its access token.</summary>
    private static async Task<HttpClient> SignedInClientAsync(AtlasApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "Reader" },
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = Password }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();

        var tokens = await login.Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Unknown_user_returns_404_to_a_signed_in_caller()
    {
        using var factory = CreateFactory();
        using var client = await SignedInClientAsync(factory);

        var response = await client.GetAsync(
            $"/api/v1/users/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_pages_with_a_cursor_and_never_repeats_a_row()
    {
        using var factory = CreateFactory();
        using var client = await SignedInClientAsync(factory);

        // Registration is the only way to create accounts now.
        for (var index = 0; index < 5; index++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = UniqueEmail(), password = Password, displayName = $"Paged {index}" },
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        var seen = new List<Guid>();
        string? cursor = null;

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
    private sealed record Page<TItem>(IReadOnlyList<TItem> Items, string? NextCursor);
}
