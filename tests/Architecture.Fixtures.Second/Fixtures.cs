using Tools.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fixtures.Second
{
    public static class SecondModule
    {
        public static IModule Create() => new SecondImplementation();
        public static string Leak(string value) => value?.Trim() ?? string.Empty;
    }

    internal sealed class SecondImplementation : IModule
    {
        public string Id => "Second";
        public IReadOnlyCollection<string> RequiredDependencies => [];
        public void RegisterServices(IServiceCollection services) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment environment) { }
    }
}

namespace Fixtures.Second.Domain
{
    internal sealed class InvalidDomain(Fixtures.Second.Infrastructure.Storage storage, Microsoft.EntityFrameworkCore.DbContext db, IEndpointRouteBuilder endpoints)
    {
        public object[] Dependencies => [storage, db, endpoints];
    }
}

namespace Fixtures.Second.Application
{
    internal sealed record Service(string Value);
    internal sealed class InvalidApplication(Fixtures.Second.Presentation.Endpoint endpoint)
    {
        public object Dependency => endpoint;
    }

    internal sealed class CrossModule(Fixtures.First.Infrastructure.Secret secret, Fixtures.First.PublicApi.Contract contract, Fixtures.First.PublicApi.HiddenContract hidden)
    {
        public object[] Dependencies => [secret, contract, hidden];
    }
}

namespace Fixtures.Second.Infrastructure
{
    internal sealed record Storage(string Value);
    internal sealed record AllowedDependency(Fixtures.Second.Application.Service Service);
}

namespace Fixtures.Second.Presentation
{
    internal sealed record Endpoint(string Value);
    internal sealed record AllowedDependency(Fixtures.Second.Application.Service Service);
}

namespace Fixtures.Second.Leak
{
    public sealed record PublicImplementation(string Value);
}
