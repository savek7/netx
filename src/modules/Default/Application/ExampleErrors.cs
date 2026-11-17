using Tools.Results;

namespace Modules.Defaults.Application;

internal static class ExampleErrors
{
    public static readonly Error NotFound = new(
        "example.not_found",
        "Example was not found.",
        ErrorCategory.NotFound);
}
