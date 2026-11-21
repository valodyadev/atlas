using System.Reflection;
using Atlas.SharedKernel.Primitives;
using NetArchTest.Rules;
using Shouldly;

namespace Atlas.Architecture.Tests;

/// <summary>
/// The architecture rules from docs/architecture.md, enforced.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly SharedKernel = typeof(Result).Assembly;
    private static readonly Assembly IdentityAssembly = typeof(Modules.Identity.IdentityModule).Assembly;

    private static readonly string[] ModuleNamespacePrefixes =
    [
        "Atlas.Modules.Identity",
        // Add each new module here. A module missing from this list is not
        // checked against its siblings, so the list is part of the rule.
    ];

    [Fact]
    public void SharedKernel_has_no_infrastructure_dependencies()
    {
        // The domain kernel must stay transport- and persistence-agnostic: the
        // moment it can see EF Core or ASP.NET Core, domain logic starts
        // leaking into them and unit tests start needing a database.
        var referenced = SharedKernel
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldNotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        referenced.ShouldNotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        referenced.ShouldNotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void Modules_do_not_reference_each_other()
    {
        foreach (var moduleAssembly in new[] { IdentityAssembly })
        {
            var ownName = moduleAssembly.GetName().Name!;

            var foreignModules = ModuleNamespacePrefixes
                .Where(prefix => !ownName.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();

            if (foreignModules.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(moduleAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(foreignModules)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"{ownName} must not depend on another module. Offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void Module_domain_layer_does_not_depend_on_its_own_infrastructure()
    {
        // Dependencies point inward. Domain types that know about DbContext or
        // Npgsql cannot be unit-tested and cannot be reused behind a different
        // store.
        var result = Types.InAssembly(IdentityAssembly)
            .That()
            .ResideInNamespaceStartingWith("Atlas.Modules.Identity.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Atlas.Modules.Identity.Infrastructure",
                "Atlas.Modules.Identity.Endpoints",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Identity domain must not depend on infrastructure. Offenders: "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_host_owns_no_persistence()
    {
        // Persistence belongs to whichever module owns the schema. If the host
        // can reach EF Core directly, cross-module queries become possible and
        // extracting a module stops being a local change.
        var referenced = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldNotContain("Microsoft.EntityFrameworkCore");
        referenced.ShouldNotContain("Npgsql");
    }

    [Fact]
    public void Every_module_is_registered_with_a_distinct_name()
    {
        // Module names end up in telemetry and health-check output, so a
        // duplicate silently merges two modules' signals.
        var modules = new[] { new Modules.Identity.IdentityModule() };

        var names = modules.Select(module => module.Name).ToArray();

        names.ShouldAllBe(name => !string.IsNullOrWhiteSpace(name));
        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Length);
    }
}
