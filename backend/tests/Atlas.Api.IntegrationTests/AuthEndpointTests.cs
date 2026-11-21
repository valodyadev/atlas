using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// The authentication flows, end to end against a real PostgreSQL and the
/// real host.
/// </summary>
/// <param name="fixture">Shared database fixture.</param>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthEndpointTests(PostgresFixture fixture)
{
    private const string GoodPassword = "correct horse battery staple";

    private AtlasApiFactory CreateFactory()
    {
        Assert.SkipUnless(fixture.IsAvailable, ContainerRuntime.SkipReason);
        return new AtlasApiFactory(fixture.ConnectionString!);
    }

    private static string UniqueEmail() => $"auth-{Guid.CreateVersion7():N}@example.com";

    private sealed record Tokens(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType);
    private sealed record Ack(string Message);

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName = "Test Person" },
            TestContext.Current.CancellationToken);

    private static async Task<Tokens> RegisterAndLoginAsync(HttpClient client, string email)
    {
        (await RegisterAsync(client, email, GoodPassword)).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = GoodPassword }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken))!;
    }

    // ---------------------------------------------------------------- register

    [Fact]
    public async Task Registration_succeeds_and_then_the_account_can_sign_in()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var tokens = await RegisterAndLoginAsync(client, UniqueEmail());

        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokens.TokenType.ShouldBe("Bearer");
        tokens.ExpiresIn.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Registering_an_existing_address_is_indistinguishable_from_a_new_one()
    {
        // The whole point: a caller must not be able to discover which
        // addresses have accounts.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        var first = await RegisterAsync(client, email, GoodPassword);
        var second = await RegisterAsync(client, email, "a completely different password");

        second.StatusCode.ShouldBe(first.StatusCode);
        (await second.Content.ReadFromJsonAsync<Ack>(TestContext.Current.CancellationToken))!.Message
            .ShouldBe((await first.Content.ReadFromJsonAsync<Ack>(TestContext.Current.CancellationToken))!.Message);
    }

    [Fact]
    public async Task The_second_registration_does_not_overwrite_the_first_password()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();

        await RegisterAsync(client, email, GoodPassword);
        await RegisterAsync(client, email, "an attacker chosen password");

        // The original password still works...
        var original = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = GoodPassword }, TestContext.Current.CancellationToken);
        original.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...and the one supplied by the second attempt does not.
        var attacker = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "an attacker chosen password" }, TestContext.Current.CancellationToken);
        attacker.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("not-an-email", "correct horse battery staple")]
    [InlineData("fine@example.com", "short")]
    public async Task Malformed_registration_is_rejected_with_400(string email, string password)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await RegisterAsync(client, email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // ------------------------------------------------------------------- login

    [Fact]
    public async Task An_unknown_account_and_a_wrong_password_fail_identically()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var known = UniqueEmail();
        await RegisterAsync(client, known, GoodPassword);

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = known, password = "wrong password entirely" }, TestContext.Current.CancellationToken);
        var unknownAccount = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = UniqueEmail(), password = GoodPassword }, TestContext.Current.CancellationToken);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownAccount.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // traceId is per-request by design, so it is the one field allowed to
        // differ. Everything a caller could learn from must be identical.
        static string WithoutTraceId(string body) =>
            System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");

        WithoutTraceId(await wrongPassword.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(WithoutTraceId(await unknownAccount.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_with_the_right_password()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, GoodPassword);

        // Threshold is 3 in the test host.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "wrong" }, TestContext.Current.CancellationToken);
        }

        var correct = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = GoodPassword }, TestContext.Current.CancellationToken);

        correct.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ----------------------------------------------------------------- refresh

    [Fact]
    public async Task Refresh_returns_a_new_pair_and_retires_the_old_token()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginAsync(client, UniqueEmail());

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = (await response.Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken))!;
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
        rotated.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Reusing_a_spent_refresh_token_revokes_the_whole_family()
    {
        // The reason for rotating at all: a replayed token means the token was
        // probably stolen, so every descendant of that login is killed.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginAsync(client, UniqueEmail());

        var first = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        var rotated = (await first.Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken))!;

        // Replay the original, already-spent token.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The legitimate successor must now be dead too.
        var successor = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = rotated.RefreshToken }, TestContext.Current.CancellationToken);
        successor.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "not-a-real-token" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ logout

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginAsync(client, UniqueEmail());

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }, TestContext.Current.CancellationToken);
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logging_out_an_unknown_token_still_reports_success()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = "never-existed" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ----------------------------------------------------------- authorization

    [Fact]
    public async Task A_protected_endpoint_rejects_an_anonymous_caller()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/users", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_valid_access_token_identifies_the_caller()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var email = UniqueEmail();
        var tokens = await RegisterAndLoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(email);
        body.ShouldContain("member");
    }

    [Fact]
    public async Task An_expired_access_token_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateExpiredAccessToken());

        (await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_access_token_signed_with_the_wrong_key_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateTokenSignedWith("a-totally-different-key-32-bytes-long!!"));

        (await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_refresh_token_is_not_accepted_as_a_bearer_token()
    {
        // They are different credentials with different lifetimes; confusing
        // them would hand a 30-day token the power of a 15-minute one.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tokens = await RegisterAndLoginAsync(client, UniqueEmail());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.RefreshToken);

        (await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ----------------------------------------------------------------- helpers

    private static string CreateExpiredAccessToken() =>
        CreateToken(AtlasApiFactory.SigningKey, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

    private static string CreateTokenSignedWith(string key) =>
        CreateToken(key, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15));

    private static string CreateToken(string key, DateTime issuedAt, DateTime expires) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AtlasApiFactory.Issuer,
            Audience = AtlasApiFactory.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = Guid.CreateVersion7().ToString(),
                [JwtRegisteredClaimNames.Email] = "expired@example.com",
            },
        });
}
