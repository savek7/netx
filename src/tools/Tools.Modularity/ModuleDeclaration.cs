namespace Tools.Modularity;

public sealed class ModuleDeclaration
{
    private ModuleDeclaration(IModule module, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(module);
        Module = module;
        IsEnabled = isEnabled;
    }

    public IModule Module { get; }
    public bool IsEnabled { get; }

    public static ModuleDeclaration Enabled(IModule module) => new(module, true);
    public static ModuleDeclaration Disabled(IModule module) => new(module, false);
}
