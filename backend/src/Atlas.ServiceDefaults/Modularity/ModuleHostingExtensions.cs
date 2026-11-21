using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlas.ServiceDefaults.Modularity;

/// <summary>Wires modules into a host.</summary>
public static class ModuleHostingExtensions
{
    /// <summary>
    /// Registers each module's services and records them for endpoint mapping.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="modules">Modules to load, in order.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddModules(
        this IHostApplicationBuilder builder,
        params IApplicationModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(modules);

        var registry = new ModuleRegistry();

        foreach (var module in modules)
        {
            registry.Add(module);
            module.ConfigureServices(builder);
        }

        builder.Services.AddSingleton(registry);
        return builder;
    }

    /// <summary>
    /// Maps every registered module's endpoints onto <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">Route builder to map onto.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry = endpoints.ServiceProvider.GetRequiredService<ModuleRegistry>();

        foreach (var module in registry.Modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
