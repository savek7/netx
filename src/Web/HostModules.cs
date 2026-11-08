using Modules.Defaults;
using Tools.Modularity;

namespace Web;

internal static class HostModules
{
    public static ModuleConfiguration Create(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new([
            ModuleDeclaration.Enabled(DefaultModule.Create())
        ]);
    }
}
