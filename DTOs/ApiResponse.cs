using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

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
    /// Debugging detail for an unhandled exception (type, message, stack trace). Only populated
    /// by the global exception handler in Program.cs, and only while
    /// <c>Diagnostics:IncludeExceptionDetailsInResponse</c> is enabled in configuration -- see the
    /// warning next to that setting in appsettings.json. Omitted from the JSON entirely on every
    /// other response (success or a normal validation/not-found failure).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExceptionDetails? Error { get; set; }

    /// <summary>
    /// Builds a success envelope.
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string message = "Request completed successfully.")
        => new() { Success = true, Message = message, Data = data };

    /// <summary>
    /// Builds a failure envelope. <paramref name="data"/> is normally left null so no partial data leaks out.
    /// </summary>
    public static ApiResponse<T> FailResponse(string message, T? data = default, ExceptionDetails? error = null)
        => new() { Success = false, Message = message, Data = data, Error = error };
}

/// <summary>
/// Debugging detail about an unhandled exception, for temporary inclusion in a 500 response.
/// </summary>
/// <remarks>
/// This intentionally mirrors information a client should not normally see (stack traces, raw SQL
/// error text). It exists so the current staging investigation doesn't require pulling the Logs
/// folder for every failed call -- turn it off (see appsettings.json) once that's no longer needed.
/// </remarks>
public class ExceptionDetails
{
    /// <summary>Fully-qualified exception type name, e.g. <c>Microsoft.Data.SqlClient.SqlException</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    /// <summary>
    /// SQL Server error number (see <see cref="SqlException.Number"/>), populated only when the
    /// exception is a <see cref="SqlException"/>. Common values: -1/2 = connection/timeout,
    /// 18456 = login failed, 2812 = stored procedure not found.
    /// </summary>
    public int? SqlErrorNumber { get; set; }

    /// <summary>The wrapped inner exception, if any, built the same way.</summary>
    public ExceptionDetails? InnerException { get; set; }

    public static ExceptionDetails FromException(Exception ex) => new()
    {
        Type = ex.GetType().FullName ?? ex.GetType().Name,
        Message = ex.Message,
        StackTrace = ex.StackTrace,
        SqlErrorNumber = (ex as SqlException)?.Number,
        InnerException = ex.InnerException is not null ? FromException(ex.InnerException) : null
    };
}
