namespace Architecture.Tests;

internal static class ModuleInventory
{
    public static string[] FindMissing(IEnumerable<string> projects, IEnumerable<string> declared) =>
        projects.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
