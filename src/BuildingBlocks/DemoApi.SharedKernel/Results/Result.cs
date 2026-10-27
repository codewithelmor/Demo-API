namespace DemoApi.SharedKernel.Results;

public enum ErrorType
{
    Validation,
    InvalidToken,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    PreconditionFailed,
    PreconditionRequired,
    Unprocessable,
}

/// <summary>
/// An expected failure. <see cref="Code"/> is the stable slug used in the ProblemDetails
/// <c>type</c> (<c>/problems/{code}</c>).
/// </summary>
public sealed record Error(string Code, string Title, string Detail, ErrorType Type)
{
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public IReadOnlyDictionary<string, object?>? Extensions { get; init; }

    public static Error NotFound(string detail = "The resource was not found.") =>
        new("not-found", "Not found", detail, ErrorType.NotFound);

    public static Error Validation(string field, string message) =>
        new("validation-failed", "Validation failed", "One or more fields are invalid.", ErrorType.Validation)
        {
            ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] },
        };

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation-failed", "Validation failed", "One or more fields are invalid.", ErrorType.Validation)
        {
            ValidationErrors = errors,
        };

    public static Error Conflict(string code, string title, string detail) =>
        new(code, title, detail, ErrorType.Conflict);

    public static readonly Error PreconditionRequired =
        new("precondition-required", "Precondition required", "Send an If-Match header with the current ETag.", ErrorType.PreconditionRequired);

    public static readonly Error PreconditionFailed =
        new("precondition-failed", "Precondition failed", "The resource was changed by someone else. Reload it and try again.", ErrorType.PreconditionFailed);

    public static readonly Error ConcurrencyConflict =
        Conflict("concurrency-conflict", "Concurrency conflict", "The resource was changed at the same time. Try again.");

    public static readonly Error InvalidToken =
        new("invalid-token", "Invalid token", "The token is invalid or has expired.", ErrorType.InvalidToken);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error)
        : base(error) => _value = value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Ok(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}