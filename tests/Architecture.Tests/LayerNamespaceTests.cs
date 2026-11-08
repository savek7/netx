using Xunit;
using Mono.Cecil;

namespace Architecture.Tests;

public sealed class LayerNamespaceTests
{
    [Fact]
    public void DomainFileCannotEscapeLayerThroughItsNamespace()
    {
        string[] errors = LayerNamespaces.Check("Fixtures.Second", "Domain/Entity.cs", "namespace Fixtures.Second; internal class Entity { }");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ExistingLayerFoldersMatchTheirNamespaces()
    {
        foreach (string project in Repository.ModuleProjects)
        {
            string directory = Path.GetDirectoryName(project)!;
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(project) + ".dll"));
            string root = assembly.MainModule.GetTypes().First(type => type.Interfaces.Any(item => item.InterfaceType.FullName == "Tools.Modularity.IModule")).Namespace;
            string[] layers = ["Domain", "Application", "Infrastructure", "Presentation", "PublicApi"];
            foreach (string layer in layers)
            {
                string folder = Path.Combine(directory, layer);
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    string[] errors = LayerNamespaces.Check(root, Path.GetRelativePath(directory, file), File.ReadAllText(file));
                    Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
                }
            }
        }
    }

    [Theory]
    [InlineData("namespace Fixtures.Second.Domain; internal class Entity { }")]
    [InlineData("namespace Fixtures { namespace Second { namespace Domain { internal class Entity { } } } }")]
    public void DomainFileCanUseMatchingNamespace(string source) =>
        Assert.Empty(LayerNamespaces.Check("Fixtures.Second", "Domain/Entity.cs", source));
}
