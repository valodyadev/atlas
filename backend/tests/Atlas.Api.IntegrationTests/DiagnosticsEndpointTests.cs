using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// The endpoints the platform itself depends on. If these break, every probe,
/// dashboard and client code generator breaks with them.
/// </summary>
/// <param name="fixture">Shared database fixture.</param>
[Collection(IntegrationTestCollection.Name)]
public sealed class DiagnosticsEndpointTests(PostgresFixture fixture)
{
    private AtlasApiFactory CreateFactory()
    {
        Assert.SkipUnless(fixture.IsAvailable, ContainerRuntime.SkipReason);
        return new AtlasApiFactory(fixture.ConnectionString!);
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_the_database()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_reports_healthy_when_the_database_is_reachable()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Meta_reports_the_environment()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/meta", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MetaResponse>(
            TestContext.Current.CancellationToken);
        body!.Environment.ShouldBe("Development");
    }

    [Fact]
    public async Task OpenApi_document_is_served_and_describes_the_user_endpoints()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        document.ShouldContain("/api/v1/users");
        document.ShouldContain("Atlas API");
    }

    private sealed record MetaResponse(string Service, string Environment, string Version);
}
