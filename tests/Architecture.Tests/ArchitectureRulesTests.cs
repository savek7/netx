using Mono.Cecil;
using Xunit;

namespace Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void ExistingModulesFollowArchitectureRules()
    {
        string[] paths = Repository.ModuleProjects;
        Assert.NotEmpty(paths);
        List<AssemblyDefinition> modules = [];
        try
        {
            foreach (string path in paths)
            {
                string assembly = Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(path) + ".dll");
                Assert.True(File.Exists(assembly), $"Module assembly not found: {assembly}. Module assembly name must match its project name.");
                modules.Add(AssemblyDefinition.ReadAssembly(assembly));
            }
            string[] errors = ArchitectureRules.Check(modules);
            Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
        }
        finally
        {
            foreach (AssemblyDefinition module in modules) module.Dispose();
        }
    }

    [Fact]
    public void ValidFixtureHasNoViolations()
    {
        using AssemblyDefinition fixture = AssemblyDefinition.ReadAssembly(typeof(Fixtures.First.FirstModule).Assembly.Location);
        Assert.Empty(ArchitectureRules.Check([fixture]));
    }

    [Fact]
    public void ProjectWithoutModuleImplementationIsRejected()
    {
        using AssemblyDefinition fixture = AssemblyDefinition.ReadAssembly(typeof(ArchitectureRulesTests).Assembly.Location);
        Assert.Contains(ArchitectureRules.Check([fixture]), x => x.Contains("missing IModule implementation", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicModuleImplementationIsRejectedEvenInPublicApi()
    {
        using AssemblyDefinition fixture = AssemblyDefinition.ReadAssembly(typeof(Fixtures.First.FirstModule).Assembly.Location);
        TypeDefinition implementation = fixture.MainModule.GetTypes().Single(type => type.Name == "FirstImplementation");
        implementation.IsPublic = true;
        implementation.Namespace = "Fixtures.First.PublicApi";
        Assert.Contains(ArchitectureRules.Check([fixture]), x => x.Contains("IModule implementation must not be public", StringComparison.Ordinal));
    }

    [Fact]
    public void ControlledFixturesExposeLayerAndModuleBoundaryViolations()
    {
        using AssemblyDefinition first = AssemblyDefinition.ReadAssembly(typeof(Fixtures.First.FirstModule).Assembly.Location);
        using AssemblyDefinition second = AssemblyDefinition.ReadAssembly(typeof(Fixtures.Second.SecondModule).Assembly.Location);
        string[] violations = ArchitectureRules.Check([first, second]);

        Assert.Contains(violations, x => x.Contains("public surface", StringComparison.Ordinal) && x.Contains("PublicImplementation", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("public surface", StringComparison.Ordinal) && x.Contains("SecondModule::Leak", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("Domain", StringComparison.Ordinal) && x.Contains("Infrastructure.Storage", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("Domain", StringComparison.Ordinal) && x.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("Domain", StringComparison.Ordinal) && x.Contains("AspNetCore", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("Application", StringComparison.Ordinal) && x.Contains("Presentation.Endpoint", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("cross-module", StringComparison.Ordinal) && x.Contains("Infrastructure.Secret", StringComparison.Ordinal));
        Assert.Contains(violations, x => x.Contains("cross-module", StringComparison.Ordinal) && x.Contains("PublicApi.HiddenContract", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, x => x.Contains("PublicApi.Contract", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, x => x.Contains("AllowedDependency", StringComparison.Ordinal));
    }
}
