using Microsoft.AspNetCore.Routing;
using Atlas.SharedKernel.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // The baseline a module may rely on without the host having to
        // remember. AddServiceDefaults registers these too; TryAdd keeps the
        // two paths from fighting and makes the order irrelevant.
        //
        // This exists because the DbMigrator did NOT call AddServiceDefaults
        // and crashed at startup resolving IClock for the Identity module's
        // UserService. A host that forgets the baseline should not be able to
        // break a module.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<IClock, SystemClock>();

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
