using Modules.Defaults.Application;
using Modules.Defaults.Infrastructure;
using Modules.Defaults.Presentation;
using Modules.Defaults.PublicApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tools.Modularity;

namespace Modules.Defaults;

public static class DefaultModule
{
    public static IModule Create() => new DefaultModuleImplementation();
}

internal sealed class DefaultModuleImplementation : IModule
{
    public string Id => "Default";
    public IReadOnlyCollection<string> RequiredDependencies => Array.Empty<string>();

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IExampleRepository>(_ => new InMemoryExampleRepository());
        services.AddSingleton(provider => new ExampleService(provider.GetRequiredService<IExampleRepository>()));
        services.AddSingleton<IExampleApi>(provider => new ExampleApi(provider.GetRequiredService<ExampleService>()));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsDevelopment())
        {
            ExampleEndpoints.Map(endpoints);
        }
    }
}
