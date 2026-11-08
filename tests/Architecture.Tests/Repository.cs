namespace Architecture.Tests;

internal static class Repository
{
    public static string[] ModuleProjects => Directory.GetFiles(Path.Combine(Root, "src", "modules"), "*.csproj", SearchOption.AllDirectories);

    public static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "netx.slnx")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Could not find netx.slnx.");
        }
    }
}
