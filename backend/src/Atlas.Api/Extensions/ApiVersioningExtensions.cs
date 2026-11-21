using Asp.Versioning;

namespace Atlas.Api.Extensions;

/// <summary>
/// URL-segment API versioning (<c>/api/v1/...</c>).
/// </summary>
/// <remarks>
/// A mobile client that is already on a user's phone cannot be forced to
/// upgrade, so the API must be able to serve two shapes at once. Versioning
/// from day one costs nothing; retrofitting it after the first App Store
/// release is a migration project.
/// </remarks>
internal static class ApiVersioningExtensions
{
    internal static IServiceCollection AddApiVersioningDefaults(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }
}
