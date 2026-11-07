using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.Defaults;
using Modules.Defaults.PublicApi;
using Tools.Modularity;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class ModuleConfigurationTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", false)]
    public async Task ConfigurationSelectsModulesByEnvironment(string environment, bool enabled)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        IModule module = DefaultModule.Create();
        ModuleConfiguration configuration = new([
            builder.Environment.IsDevelopment() ? ModuleDeclaration.Enabled(module) : ModuleDeclaration.Disabled(module)
        ]);
        Assert.Equal(enabled, Assert.Single(configuration.Declarations).IsEnabled);
        configuration.RegisterServices(builder.Services);
        Assert.Equal(enabled, builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IExampleApi)));
        await using WebApplication app = builder.Build();
        configuration.MapEndpoints(app, app.Environment);
        await app.StartAsync();
        string[] mappedRoutes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToArray();
        Assert.Equal(enabled, mappedRoutes.Any(route => route.TrimEnd('/').Equals("/default/examples", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task DisabledModuleDoesNotRegisterServicesOrMapEndpointsAlongsideActiveModule()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        TestModule active = new("Active");
        TestModule disabled = new("Disabled");
        ModuleConfiguration configuration = new([
            ModuleDeclaration.Enabled(active),
            ModuleDeclaration.Disabled(disabled)
        ]);
        configuration.RegisterServices(builder.Services);
        await using WebApplication app = builder.Build();
        configuration.MapEndpoints(app, app.Environment);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage activeResponse = await client.GetAsync(new Uri("/Active", UriKind.Relative));
        using HttpResponseMessage disabledResponse = await client.GetAsync(new Uri("/Disabled", UriKind.Relative));

        Assert.Equal(["Active"], app.Services.GetServices<Marker>().Select(marker => marker.Id));
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabledResponse.StatusCode);
        Assert.Equal(0, disabled.Registrations);
        Assert.Equal(0, disabled.Mappings);
    }

    [Fact]
    public void RequiredActiveDependencyCanBeDeclaredAfterItsConsumer()
    {
        TestModule consumer = new("Consumer", "Dependency");
        TestModule dependency = new("Dependency");
        ModuleConfiguration configuration = new([ModuleDeclaration.Enabled(consumer), ModuleDeclaration.Enabled(dependency)]);
        ServiceCollection services = new();

        configuration.RegisterServices(services);

        Assert.Equal(1, consumer.Registrations);
        Assert.Equal(1, dependency.Registrations);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "disabled")]
    public void MissingOrDisabledDependencyFailsBeforeAnyServiceRegistration(bool declareDisabled, string expected)
    {
        TestModule first = new("First");
        TestModule consumer = new("Consumer", "Dependency");
        List<ModuleDeclaration> declarations = [ModuleDeclaration.Enabled(first), ModuleDeclaration.Enabled(consumer)];
        if (declareDisabled)
        {
            declarations.Add(ModuleDeclaration.Disabled(new TestModule("Dependency")));
        }
        ServiceCollection services = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ModuleConfiguration(declarations).RegisterServices(services));

        Assert.Contains("Consumer", error.Message, StringComparison.Ordinal);
        Assert.Contains("Dependency", error.Message, StringComparison.Ordinal);
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
        Assert.Equal(0, first.Registrations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DuplicateIdsIncludingDisabledDeclarationsFailBeforeRegistration(bool secondEnabled)
    {
        TestModule first = new("Duplicate");
        TestModule second = new("Duplicate");
        ServiceCollection services = new();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ModuleConfiguration([
            ModuleDeclaration.Enabled(first),
            secondEnabled ? ModuleDeclaration.Enabled(second) : ModuleDeclaration.Disabled(second)
        ]).RegisterServices(services));

        Assert.Contains("Duplicate module ID 'Duplicate'", error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
        Assert.Equal(0, first.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CyclesFailBeforeRegistrationWithDependencyPath(bool selfCycle)
    {
        TestModule first = new("First", selfCycle ? "First" : "Second");
        TestModule second = new("Second", "Third");
        TestModule third = new("Third", "First");
        ServiceCollection services = new();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ModuleConfiguration([
            ModuleDeclaration.Enabled(first), ModuleDeclaration.Enabled(second), ModuleDeclaration.Enabled(third)
        ]).RegisterServices(services));

        Assert.Contains(selfCycle ? "First -> First" : "First -> Second -> Third -> First", error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
        Assert.Equal(0, first.Registrations);
    }

    [Fact]
    public void DisabledModuleDependenciesAreNotRequired()
    {
        ModuleConfiguration configuration = new([ModuleDeclaration.Disabled(new TestModule("Disabled", "Missing"))]);
        ServiceCollection services = new();
        configuration.RegisterServices(services);
        Assert.Empty(services);
    }

    [Fact]
    public void SharedDependencyIsNotACycle()
    {
        ModuleConfiguration configuration = new([
            ModuleDeclaration.Enabled(new TestModule("Root", "Left", "Right")),
            ModuleDeclaration.Enabled(new TestModule("Left", "Shared")),
            ModuleDeclaration.Enabled(new TestModule("Right", "Shared")),
            ModuleDeclaration.Enabled(new TestModule("Shared"))
        ]);
        ServiceCollection services = new();
        configuration.RegisterServices(services);
        Assert.Equal(4, services.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidDependencyIdsAreRejected(string dependency)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ModuleConfiguration([
            ModuleDeclaration.Enabled(new TestModule("Consumer", dependency))
        ]));
        Assert.Contains("Consumer", error.Message, StringComparison.Ordinal);
        Assert.Contains("invalid dependency ID", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidModuleIdsAreRejected(string id)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ModuleConfiguration([ModuleDeclaration.Enabled(new TestModule(id))]));
        Assert.Contains("module ID", error.Message, StringComparison.Ordinal);
    }

    private sealed record Marker(string Id);

    private sealed class TestModule(string id, params string[] dependencies) : IModule
    {
        public string Id => id;
        public IReadOnlyCollection<string> RequiredDependencies => dependencies;
        public int Registrations { get; private set; }
        public int Mappings { get; private set; }

        public void RegisterServices(IServiceCollection services)
        {
            Registrations++;
            services.AddSingleton(new Marker(Id));
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment)
        {
            Mappings++;
            endpoints.MapGet($"/{Id}", () => Id);
        }
    }
}
