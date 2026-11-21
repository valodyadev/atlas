using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Atlas.Api.Extensions;

/// <summary>
/// OpenAPI document generation and the interactive reference UI.
/// </summary>
/// <remarks>
/// Documents are generated from the endpoints themselves, so the spec cannot
/// drift from the implementation. CI exports the document to
/// <c>contracts/openapi/</c> and diffs it, which turns an accidental breaking
/// change into a failed build rather than a broken iOS release.
/// </remarks>
internal static class OpenApiExtensions
{
    internal const string DocumentName = "v1";

    internal static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Atlas API",
                    Version = "v1",
                    Description = "Public HTTP API for the Atlas platform.",
                };

                SubstituteApiVersionInPaths(document);
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// Replaces the <c>v{version}</c> route placeholder with the concrete
    /// version in every path.
    /// </summary>
    /// <remarks>
    /// Asp.Versioning routes on <c>/api/v{version:apiVersion}</c>, which is
    /// what makes serving two versions side by side a one-line change. Left
    /// as-is in the document, however, every client generator would emit a
    /// pointless <c>version</c> parameter on every single call. Substituting
    /// here keeps the routing flexible and the published contract honest.
    /// </remarks>
    /// <param name="document">The document being generated.</param>
    private static void SubstituteApiVersionInPaths(OpenApiDocument document)
    {
        const string placeholder = "v{version}";
        var concrete = DocumentName;

        var rewritten = document.Paths
            .Where(entry => entry.Key.Contains(placeholder, StringComparison.Ordinal))
            .ToList();

        foreach (var entry in rewritten)
        {
            document.Paths.Remove(entry.Key);
            document.Paths.Add(
                entry.Key.Replace(placeholder, concrete, StringComparison.Ordinal),
                entry.Value);
        }
    }

    internal static WebApplication MapApiDocumentation(this WebApplication app)
    {
        // The spec itself: /openapi/v1.json
        app.MapOpenApi().AllowAnonymous();

        // Human-facing reference UI. Exposed only outside production; in
        // production the spec is published from contracts/openapi/ instead.
        if (!app.Environment.IsProduction())
        {
            app.MapScalarApiReference(options => options
                .WithTitle("Atlas API")
                .WithOpenApiRoutePattern($"/openapi/{DocumentName}.json"))
                .AllowAnonymous();
        }

        return app;
    }
}
