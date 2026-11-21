using Modules.Defaults;
using Tools.Mediation.MediatR;
using Tools.Modularity;

namespace Web;

internal static class HostModules
{
    public static void RegisterServices(IServiceCollection services, ModuleConfiguration configuration)
    {
        IEnumerable<Type> messageCandidates = configuration.Declarations
            .Where(declaration => declaration.IsEnabled)
            .Select(declaration => declaration.Module.GetType().Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes());

        services.AddMediation(messageCandidates);
        configuration.RegisterServices(services);
    }

    public static ModuleConfiguration Create(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new([
            ModuleDeclaration.Enabled(DefaultModule.Create())
        ]);
    }
}
