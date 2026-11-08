using Tools.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fixtures.First
{
    public static class FirstModule
    {
        public static IModule Create() => new FirstImplementation();
    }

    internal sealed class FirstImplementation : IModule
    {
        public string Id => "First";
        public IReadOnlyCollection<string> RequiredDependencies => [];
        public void RegisterServices(IServiceCollection services) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment) { }
    }
}

namespace Fixtures.First.PublicApi
{
    internal sealed record HiddenContract(string Value);
    public sealed record Contract(string Value)
    {
        public sealed record Nested(string Value);
    }
}

namespace Fixtures.First.Infrastructure
{
    internal sealed record Secret(string Value);
}
