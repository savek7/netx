using Mono.Cecil;

namespace Architecture.Tests;

internal static class ProjectBoundaries
{
    public static Dictionary<string, string[]> AssemblyEdges(IReadOnlyDictionary<string, AssemblyDefinition> assemblies)
    {
        Dictionary<string, string> projectsByName = assemblies.ToDictionary(pair => pair.Value.Name.Name, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        return assemblies.ToDictionary(pair => pair.Key, pair => pair.Value.MainModule.AssemblyReferences
            .Where(reference => projectsByName.ContainsKey(reference.Name))
            .Select(reference => projectsByName[reference.Name]).ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public static string[] Check(IReadOnlyDictionary<string, string[]> graph, IEnumerable<string> modules, IEnumerable<string> tools, string host)
    {
        string[] moduleProjects = modules.ToArray();
        List<string> errors = [];
        foreach (string module in moduleProjects)
            if (Reaches(graph, module, host)) errors.Add($"module -> host: {module} -> {host}");
        foreach (string tool in tools)
            foreach (string module in moduleProjects)
                if (Reaches(graph, tool, module)) errors.Add($"tool -> module: {tool} -> {module}");
        return errors.ToArray();
    }

    private static bool Reaches(IReadOnlyDictionary<string, string[]> graph, string start, string target)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> pending = new();
        pending.Push(start);
        while (pending.TryPop(out string? current))
        {
            if (!visited.Add(current) || !graph.TryGetValue(current, out string[]? dependencies)) continue;
            foreach (string dependency in dependencies)
            {
                if (string.Equals(dependency, target, StringComparison.OrdinalIgnoreCase)) return true;
                pending.Push(dependency);
            }
        }
        return false;
    }
}
