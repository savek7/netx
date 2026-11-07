using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tools.Modularity;

public interface IModule
{
    string Id { get; }
    string MigrationsTableName => "Migrations";
    IReadOnlyCollection<string> RequiredDependencies { get; }
    void RegisterServices(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment);
}
