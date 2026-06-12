namespace AspireWebAppTemplate.Core.Common;

/// <summary>
/// Represents the outcome of an API operation that does not return data.
/// Use this as the standard return type for create, update, and delete operations.
/// </summary>
public class ApiResult
{
    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Error message from the API when the operation failed. Null on success.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ApiResult Success() => new() { Succeeded = true };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static ApiResult Failure(string? error) => new() { Succeeded = false, Error = error };

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static ApiResult<T> Success<T>(T data) => new() { Succeeded = true, Data = data };

    /// <summary>
    /// Creates a failed result with data type for generic method chaining.
    /// </summary>
    public static ApiResult<T> Failure<T>(string? error) => new() { Succeeded = false, Error = error };
}

/// <summary>
/// Represents the outcome of an API operation that returns data of type <typeparamref name="T"/>.
/// Use this as the standard return type for read operations (GET endpoints).
/// </summary>
/// <typeparam name="T">The type of data returned on success.</typeparam>
public class ApiResult<T> : ApiResult
{
    /// <summary>
    /// The data payload returned by the API on success. Null when <see cref="ApiResult.Succeeded"/> is false.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static ApiResult<T> Success(T data) => new() { Succeeded = true, Data = data };

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public new static ApiResult<T> Failure(string? error) => new() { Succeeded = false, Error = error };
}
