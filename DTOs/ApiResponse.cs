namespace ReraGIS.Api.DTOs;

/// <summary>
/// Standard response envelope returned by every endpoint in this API.
/// </summary>
/// <typeparam name="T">The type of the payload carried in <see cref="Data"/>.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether the request completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A human-readable message describing the result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The response payload. Null (or an empty collection) when there is nothing to return.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Builds a success envelope.
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string message = "Request completed successfully.")
        => new() { Success = true, Message = message, Data = data };

    /// <summary>
    /// Builds a failure envelope. <paramref name="data"/> is normally left null so no partial data leaks out.
    /// </summary>
    public static ApiResponse<T> FailResponse(string message, T? data = default)
        => new() { Success = false, Message = message, Data = data };
}
