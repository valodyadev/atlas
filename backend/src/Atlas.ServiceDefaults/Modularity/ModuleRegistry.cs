namespace Atlas.ServiceDefaults.Modularity;

/// <summary>
/// The modules loaded into this host, in registration order.
/// </summary>
/// <remarks>
/// Modules are listed explicitly in the composition root rather than
/// discovered by assembly scanning. Explicit registration keeps startup
/// deterministic, keeps cold start fast, makes the dependency graph greppable,
/// and means deleting a module is a compile error rather than a silent
/// behaviour change.
/// </remarks>
public sealed class ModuleRegistry
{
    private readonly List<IApplicationModule> _modules = [];

    /// <summary>The registered modules.</summary>
    public IReadOnlyList<IApplicationModule> Modules => _modules;

    internal void Add(IApplicationModule module) => _modules.Add(module);
}
