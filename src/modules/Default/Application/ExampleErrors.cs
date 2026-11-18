using Tools.Results;

namespace Modules.Defaults.Application;

internal static class ExampleErrors
{
    public static readonly Error InvalidName = new(
        "example.invalid_name",
        "Example name must not be blank.",
        ErrorCategory.Validation);

    public static readonly Error NotFound = new(
        "example.not_found",
        "Example was not found.",
        ErrorCategory.NotFound);
}
