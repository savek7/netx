using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Modules.Defaults;
using Modules.Defaults.Application;
using Modules.Defaults.PublicApi;
using Tools.Mediation.Abstractions;
using Tools.Modularity;
using Tools.Mediation.MediatR;
using Tools.Results;
using Web;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class ModuleConfigurationTests
{
    [Fact]
    public async Task MissingHandlerPreventsHostStartup()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ModuleConfiguration configuration = new([ModuleDeclaration.Enabled(DefaultModule.Create())]);
        HostModules.RegisterServices(builder.Services, configuration);
        builder.Services.RemoveAll<IQueryHandler<GetExampleById, ExampleRecord>>();
        await using WebApplication app = builder.Build();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        AssertHandlerConfigurationError(error, typeof(GetExampleById), "No handler");
    }

    [Fact]
    public async Task DuplicateHandlersPreventHostStartup()
    {
        await using WebApplicationFactory<Program> factory = new();
        await using WebApplicationFactory<Program> duplicate = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddCommandHandler<CreateExample, CreateExampleHandler, ExampleRecord>()));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => duplicate.CreateClient());

        AssertHandlerConfigurationError(error, typeof(CreateExample), "Multiple handlers (2)");
    }

    [Fact]
    public async Task MissingHandlerForCommandWithoutValuePreventsHostStartup()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMediation([typeof(NoValueCommand)]);
        await using WebApplication app = builder.Build();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        AssertHandlerConfigurationError(error, typeof(NoValueCommand), "No handler");
    }

    [Fact]
    public async Task DuplicateHandlersForClosedGenericQueryPreventHostStartup()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMediation([typeof(GenericQuery<>)]);
        builder.Services.AddQueryHandler<GenericQuery<int>, GenericQueryHandler<int>, int>();
        builder.Services.AddQueryHandler<GenericQuery<int>, GenericQueryHandler<int>, int>();
        await using WebApplication app = builder.Build();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        AssertHandlerConfigurationError(error, typeof(GenericQuery<int>), "Multiple handlers (2)");
    }

    [Fact]
    public async Task MatchingOpenGenericHandlerCountsAsDuplicate()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMediation([typeof(NoValueCommand)]);
        builder.Services.AddScoped(_ => new ScopedDependency(Guid.NewGuid()));
        builder.Services.AddCommandHandler<NoValueCommand, ScopedHandler>();
        builder.Services.AddScoped(typeof(ICommandHandler<>), typeof(OpenCommandHandler<>));
        await using WebApplication app = builder.Build();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        AssertHandlerConfigurationError(error, typeof(NoValueCommand), "Multiple handlers (2)");
    }

    [Fact]
    public async Task ValidHostStartsWithoutResolvingScopedDependencyAndUsesInvocationScope()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Services.AddMediation([typeof(ScopedCommand), typeof(NoValueCommand)]);
        builder.Services.AddCommandHandler<ScopedCommand, ScopedHandler, Guid>();
        builder.Services.AddCommandHandler<NoValueCommand, ScopedHandler>();
        int scopedInstances = 0;
        builder.Services.AddScoped(_ =>
        {
            scopedInstances++;
            return new ScopedDependency(Guid.NewGuid());
        });
        await using WebApplication app = builder.Build();

        await app.StartAsync();

        Assert.Equal(0, scopedInstances);
        using IServiceScope firstScope = app.Services.CreateScope();
        using IServiceScope secondScope = app.Services.CreateScope();
        ICommandMediator firstMediator = firstScope.ServiceProvider.GetRequiredService<ICommandMediator>();
        ICommandMediator secondMediator = secondScope.ServiceProvider.GetRequiredService<ICommandMediator>();
        Guid firstId = (await firstMediator.SendAsync(new ScopedCommand())).Value;
        Guid secondId = (await secondMediator.SendAsync(new ScopedCommand())).Value;
        Assert.Equal(firstScope.ServiceProvider.GetRequiredService<ScopedDependency>().Id, firstId);
        Assert.Equal(secondScope.ServiceProvider.GetRequiredService<ScopedDependency>().Id, secondId);
        Assert.NotEqual(firstId, secondId);
        Assert.True((await firstMediator.SendAsync(new NoValueCommand())).IsSuccess);
    }

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
        HostModules.RegisterServices(builder.Services, configuration);
        Assert.Equal(enabled, builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IExampleApi)));
        Assert.Equal(enabled, builder.Services.Any(descriptor => descriptor.ServiceType == typeof(Tools.Mediation.Abstractions.IQueryHandler<Modules.Defaults.Application.GetExampleById, ExampleRecord>)));
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
    public async Task DisabledModuleWithMissingAndDuplicateHandlersDoesNotBlockActiveModule()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ModuleConfiguration configuration = new([
            ModuleDeclaration.Enabled(DefaultModule.Create()),
            ModuleDeclaration.Disabled(new InvalidMediationModule())
        ]);
        HostModules.RegisterServices(builder.Services, configuration);
        await using WebApplication app = builder.Build();

        await app.StartAsync();

        using IServiceScope scope = app.Services.CreateScope();
        Assert.True(app.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IExampleApi)));
        Assert.Empty(scope.ServiceProvider.GetServices<ICommandHandler<ScopedCommand, Guid>>());
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

    private static void AssertHandlerConfigurationError(InvalidOperationException error, Type messageType, string diagnostic)
    {
        Assert.Contains($"{diagnostic} registered for '{messageType.FullName}'", error.Message, StringComparison.Ordinal);
    }

    private sealed record Marker(string Id);

    private sealed record NoValueCommand : ICommand;

    private sealed record ScopedCommand : ICommand<Guid>;

    private sealed record ScopedDependency(Guid Id);

    private readonly record struct GenericQuery<T>(T Value) : IQuery<T>;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
    private sealed class GenericQueryHandler<T> : IQueryHandler<GenericQuery<T>, T>
    {
        public Task<Result<T>> HandleAsync(GenericQuery<T> query, CancellationToken cancellationToken)
            => Task.FromResult(Result<T>.Success(query.Value));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
    private sealed class OpenCommandHandler<T> : ICommandHandler<T> where T : ICommand
    {
        public Task<Result> HandleAsync(T command, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success());
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
    private sealed class ScopedHandler(ScopedDependency dependency)
        : ICommandHandler<ScopedCommand, Guid>, ICommandHandler<NoValueCommand>
    {
        private readonly ScopedDependency dependency = dependency;

        public Task<Result<Guid>> HandleAsync(ScopedCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result<Guid>.Success(dependency.Id));

        public Task<Result> HandleAsync(NoValueCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success());
    }

    private sealed class InvalidMediationModule : IModule
    {
        public string Id => "InvalidMediation";
        public IReadOnlyCollection<string> RequiredDependencies => [];

        public void RegisterServices(IServiceCollection services)
        {
            services.AddScoped(_ => new ScopedDependency(Guid.NewGuid()));
            services.AddCommandHandler<ScopedCommand, ScopedHandler, Guid>();
            services.AddCommandHandler<ScopedCommand, ScopedHandler, Guid>();
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment)
        {
        }
    }

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
