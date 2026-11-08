using Xunit;
using Mono.Cecil;

namespace Architecture.Tests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void DependenciesThroughOtherProjectsAreDetected()
    {
        Dictionary<string, string[]> graph = new(StringComparer.Ordinal)
        {
            ["module"] = ["bridge"], ["bridge"] = ["host"], ["tool"] = ["module"], ["host"] = []
        };
        string[] errors = ProjectBoundaries.Check(graph, ["module"], ["tool"], "host");
        Assert.Contains(errors, error => error.Contains("module -> host", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("tool -> module", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportedPropertyExpandedProjectReferencesAreEvaluated()
    {
        string project = Path.Combine(Repository.Root, "tests", "Architecture.Fixtures.Second", "Architecture.Fixtures.Second.csproj");
        string dependency = Path.Combine(Repository.Root, "tests", "Architecture.Fixtures.First", "Architecture.Fixtures.First.csproj");
        Assert.Contains(dependency, ProjectEvaluation.References(project));
    }

    [Fact]
    public void AssemblyReferencesAlsoCreateProjectEdges()
    {
        using AssemblyDefinition first = AssemblyDefinition.ReadAssembly(typeof(Fixtures.First.FirstModule).Assembly.Location);
        using AssemblyDefinition second = AssemblyDefinition.ReadAssembly(typeof(Fixtures.Second.SecondModule).Assembly.Location);
        Dictionary<string, AssemblyDefinition> assemblies = new(StringComparer.Ordinal) { ["module"] = first, ["tool"] = second };
        Dictionary<string, string[]> edges = ProjectBoundaries.AssemblyEdges(assemblies);
        Assert.Contains("module", edges["tool"]);
    }

    [Fact]
    public void ExistingProjectReferencesRespectHostAndToolsBoundaries()
    {
        string root = Path.Combine(Repository.Root, "src");
        string[] projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
        Dictionary<string, string[]> graph = projects.ToDictionary(
            path => path,
            ProjectEvaluation.References,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AssemblyDefinition> assemblies = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string project in projects)
            {
                string path = Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(project) + ".dll");
                Assert.True(File.Exists(path), $"Project assembly not found: {path}");
                assemblies.Add(project, AssemblyDefinition.ReadAssembly(path));
            }
            foreach ((string project, string[] references) in ProjectBoundaries.AssemblyEdges(assemblies))
                graph[project] = graph[project].Concat(references).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            foreach (AssemblyDefinition assembly in assemblies.Values) assembly.Dispose();
        }
        string[] modules = projects.Where(path => IsIn(path, Path.Combine(root, "modules"))).ToArray();
        string[] tools = projects.Where(path => IsIn(path, Path.Combine(root, "tools"))).ToArray();
        string[] errors = ProjectBoundaries.Check(graph, modules, tools, Path.Combine(root, "Web", "Web.csproj"));
        Assert.NotEmpty(modules);
        Assert.NotEmpty(tools);
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
    }

    private static bool IsIn(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
