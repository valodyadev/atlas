using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Atlas.ServiceDefaults.Modularity;

/// <summary>
/// A vertical slice of the system: its own domain, persistence and endpoints.
///
/// A module is the unit of ownership and the unit of extraction. Modules never
/// reference each other's internals - they talk through published contracts -
/// so any module can be lifted into its own deployable service later without
/// untangling it first. The architecture tests enforce that boundary.
/// </summary>
public interface IApplicationModule
{
    /// <summary>
    /// Short, stable module name. Used in telemetry, health check tags and
    /// log scopes, so changing it is a breaking change for dashboards.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Registers the module's services, options and DbContext.
    /// Called once at startup, before the host is built.
    /// </summary>
    /// <param name="builder">The host builder being configured.</param>
    void ConfigureServices(IHostApplicationBuilder builder);

    /// <summary>
    /// Maps the module's HTTP endpoints onto the supplied route group.
    /// </summary>
    /// <param name="endpoints">Route builder, already scoped to the API version prefix.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
