using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tools.Modularity;

public sealed class ModuleConfiguration
{
    public ModuleConfiguration(IEnumerable<ModuleDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ModuleDeclaration[] entries = declarations.ToArray();
        Validate(entries);
        Declarations = Array.AsReadOnly(entries);
    }

    public IReadOnlyList<ModuleDeclaration> Declarations { get; }

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (ModuleDeclaration entry in Declarations.Where(entry => entry.IsEnabled))
        {
            entry.Module.RegisterServices(services);
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);
        foreach (ModuleDeclaration entry in Declarations.Where(entry => entry.IsEnabled))
        {
            entry.Module.MapEndpoints(endpoints, environment);
        }
    }

    private static void Validate(ModuleDeclaration[] declarations)
    {
        Dictionary<string, ModuleDeclaration> byId = new(StringComparer.Ordinal);
        foreach (ModuleDeclaration entry in declarations)
        {
            if (entry is null)
            {
                throw new InvalidOperationException("A module declaration cannot be null.");
            }

            string id = entry.Module.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("A module ID cannot be null, empty, or whitespace.");
            }

            if (!byId.TryAdd(id, entry))
            {
                throw new InvalidOperationException($"Duplicate module ID '{id}'.");
            }
        }

        Dictionary<string, string[]> dependencies = new(StringComparer.Ordinal);
        foreach ((string id, ModuleDeclaration entry) in byId.Where(pair => pair.Value.IsEnabled))
        {
            IReadOnlyCollection<string>? requiredDependencies = entry.Module.RequiredDependencies;
            if (requiredDependencies is null)
            {
                throw new InvalidOperationException($"Module '{id}' has a null dependency list.");
            }

            string[] required = requiredDependencies.ToArray();
            foreach (string dependency in required)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    throw new InvalidOperationException($"Module '{id}' has an invalid dependency ID.");
                }

                if (!byId.TryGetValue(dependency, out ModuleDeclaration? target))
                {
                    throw new InvalidOperationException($"Module '{id}' requires missing module '{dependency}'.");
                }

                if (!target.IsEnabled)
                {
                    throw new InvalidOperationException($"Module '{id}' requires disabled module '{dependency}'.");
                }
            }

            dependencies.Add(id, required);
        }

        HashSet<string> visited = new(StringComparer.Ordinal);
        List<string> path = [];
        foreach (string id in dependencies.Keys)
        {
            Visit(id, dependencies, visited, path);
        }
    }

    private static void Visit(string id, Dictionary<string, string[]> dependencies, HashSet<string> visited, List<string> path)
    {
        int cycleStart = path.IndexOf(id);
        if (cycleStart >= 0)
        {
            IEnumerable<string> cycle = path.Skip(cycleStart).Append(id);
            throw new InvalidOperationException($"Cyclic module dependency: {string.Join(" -> ", cycle)}.");
        }

        if (!visited.Add(id))
        {
            return;
        }

        path.Add(id);
        foreach (string dependency in dependencies[id])
        {
            Visit(dependency, dependencies, visited, path);
        }
        path.RemoveAt(path.Count - 1);
    }
}
