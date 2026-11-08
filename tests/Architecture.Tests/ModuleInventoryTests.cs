using Xunit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Web;
using Modules.Defaults;
using Tools.Modularity;

namespace Architecture.Tests;

public sealed class ModuleInventoryTests
{
    [Fact]
    public void MissingModuleIsReported()
    {
        string[] errors = ModuleInventory.FindMissing(["Default", "Orders"], ["Default"]);

        Assert.Equal(["Orders"], errors);
    }

    [Fact]
    public void EnabledAndDisabledDeclarationsBothCountAndRemovedModulesDoNot()
    {
        ModuleConfiguration configuration = new([ModuleDeclaration.Disabled(DefaultModule.Create())]);
        string[] declared = configuration.Declarations.Select(entry => entry.Module.GetType().Assembly.GetName().Name!).ToArray();
        Assert.Empty(ModuleInventory.FindMissing(["Default"], declared));
        Assert.Empty(ModuleInventory.FindMissing([], declared));
        Assert.Equal(["Orders"], ModuleInventory.FindMissing(["Default", "Orders"], declared));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void EveryExistingModuleIsDeclaredByTheHost(string environmentName)
    {
        string[] projects = Repository.ModuleProjects
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToArray();
        Assert.NotEmpty(projects);
        string[] declared = HostModules.Create(new TestEnvironment { EnvironmentName = environmentName }).Declarations
            .Select(entry => entry.Module.GetType().Assembly.GetName().Name!).ToArray();
        Assert.Empty(ModuleInventory.FindMissing(projects, declared));
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
