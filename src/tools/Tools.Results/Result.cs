#pragma warning disable CA1716, CA1000 // Domain terminology and discoverable typed factories.
namespace Tools.Results;

public enum ErrorCategory
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule
}

public sealed record Error
{
    public Error(string code, string description, ErrorCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        Code = code;
        Description = description;
        Category = category;
    }

    public string Code { get; }
    public string Description { get; }
    public ErrorCategory Category { get; }
}

public sealed class Result
{
    private Result(IReadOnlyList<Error> errors) => Errors = errors;

    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new(Array.Empty<Error>());

    public static Result Failure(params Error[] errors) => new(CopyFailureErrors(errors));

    internal static IReadOnlyList<Error> CopyFailureErrors(Error[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Error[] copy = (Error[])errors.Clone();
        if (copy.Length == 0 || Array.Exists(copy, error => error is null))
        {
            throw new ArgumentException("At least one non-null error is required.", nameof(errors));
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed class Result<T>
{
    private readonly T? value;

    private Result(T value)
    {
        this.value = value;
        IsSuccess = true;
        Errors = Array.Empty<Error>();
    }

    private Result(IReadOnlyList<Error> errors)
    {
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");
    public IReadOnlyList<Error> Errors { get; }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(value);
    }

    public static Result<T> Failure(params Error[] errors) => new(Result.CopyFailureErrors(errors));
}
