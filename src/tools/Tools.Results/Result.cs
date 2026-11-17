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

public sealed class Result<T>
{
    private readonly T? value;

    private Result(T value)
    {
        this.value = value;
        IsSuccess = true;
        Errors = Array.Empty<Error>();
    }

    private Result(Error[] errors)
    {
        Errors = Array.AsReadOnly(errors);
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

    public static Result<T> Failure(params Error[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Length == 0 || Array.Exists(errors, error => error is null))
        {
            throw new ArgumentException("At least one non-null error is required.", nameof(errors));
        }

        return new((Error[])errors.Clone());
    }
}
