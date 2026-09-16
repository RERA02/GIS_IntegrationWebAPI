using Microsoft.Data.SqlClient;

namespace ReraGIS.Api.Data;

/// <summary>
/// Creates <see cref="SqlConnection"/> instances for the configured database.
/// Kept as a small abstraction so services depend on an interface (testable) rather than
/// reading configuration themselves.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Creates a new, unopened <see cref="SqlConnection"/> using the application's
    /// <c>ConnectionStrings:DefaultConnection</c> setting. The caller owns the connection
    /// and is responsible for opening and disposing it (typically via <c>await using</c>).
    /// </summary>
    SqlConnection CreateConnection();
}

/// <inheritdoc cref="ISqlConnectionFactory"/>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Set it in appsettings.json, an environment variable, or User Secrets.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
