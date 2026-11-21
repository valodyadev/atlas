using System.Reflection;

namespace Atlas.Api.Endpoints;

/// <summary>
/// Build and environment metadata.
/// </summary>
/// <remarks>
/// Exists so a mobile client, a support engineer or a smoke test can confirm
/// exactly which build is serving traffic. It deliberately exposes no
/// configuration values.
/// </remarks>
internal static class MetaEndpoints
{
    internal static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/meta", (IHostEnvironment environment) => Results.Ok(new
        {
            service = environment.ApplicationName,
            environment = environment.EnvironmentName,
            version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? "unknown",
        }))
            .WithName("GetMeta")
            .WithSummary("Build and environment metadata.")
            .WithTags("Diagnostics")
            .AllowAnonymous();

        return endpoints;
    }
}
