using Modules.Defaults.Application;
using Modules.Defaults.Infrastructure;
using Modules.Defaults.Presentation;
using Modules.Defaults.PublicApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
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
        services.AddDbContext<DefaultDbContext>((provider, options) =>
        {
            string connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
            DefaultDatabase.Configure(options, connectionString, this);
        });
        services.AddScoped<IExampleRepository, ExampleRepository>();
        services.AddScoped<ExampleService>();
        services.AddScoped<IExampleApi, ExampleApi>();
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
