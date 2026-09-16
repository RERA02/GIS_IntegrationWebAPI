using System.Data;
using Microsoft.Data.SqlClient;
using ReraGIS.Api.Data;
using ReraGIS.Api.DTOs;
using ReraGIS.Api.Services.Interfaces;

namespace ReraGIS.Api.Services;

/// <inheritdoc cref="IProjectDetailsService"/>
public sealed class ProjectDetailsService : IProjectDetailsService
{
    private const string StoredProcedureName = "Get_ProjectDetailsForGIS";

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<ProjectDetailsService> _logger;

    public ProjectDetailsService(ISqlConnectionFactory connectionFactory, ILogger<ProjectDetailsService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ProjectDetailsQueryResult> GetProjectDetailsForGISAsync(
        int projectId,
        int currentLogInUserId,
        CancellationToken cancellationToken)
    {
        // `await using` guarantees the connection and command are disposed (and the connection
        // closed/returned to the pool) even if an exception is thrown below.
        await using SqlConnection connection = _connectionFactory.CreateConnection();
        await using SqlCommand command = new(StoredProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        // Parameterized -- never string-concatenated -- so this is not susceptible to SQL injection.
        // All three parameters are always supplied as real integers by the controller (each is
        // route-validated to be > 0), so DBNull.Value is not needed here; it *is* used below when
        // reading nullable result columns from the reader.        
        // section for the SQL change needed alongside this.
        command.Parameters.Add(new SqlParameter("@ProjectID", SqlDbType.Int) { Value = projectId });
        command.Parameters.Add(new SqlParameter("@CurrentLogInUserID", SqlDbType.Int) { Value = currentLogInUserId });

        await connection.OpenAsync(cancellationToken);

        // The procedure filters on the primary key (P.ID = @ProjectID), so a single row is expected.
        // CommandBehavior.SingleRow is a hint that lets the provider optimize for that case.
        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return ProjectDetailsQueryResult.NotFoundResult();
        }

        ProjectDetailsForGISDto dto = MapToDto(reader);

        // Defensive check: even though the query is expected to return at most one row, guard
        // against a data anomaly (e.g. a future change to the procedure or underlying data)
        // surfacing more than one row instead of silently dropping the extras.
        if (await reader.ReadAsync(cancellationToken))
        {
            _logger.LogWarning(
                "{StoredProcedure} returned more than one row for ProjectID {ProjectId}.",
                StoredProcedureName,
                projectId);
            return ProjectDetailsQueryResult.MultipleRowsResult();
        }

        return ProjectDetailsQueryResult.SingleResult(dto);
    }

    /// <summary>
    /// Maps the current row of <paramref name="reader"/> to a <see cref="ProjectDetailsForGISDto"/>.
    /// Column aliases containing spaces ("RERA Registration Number", "Application Status") are
    /// looked up by name via <see cref="SqlDataReader.GetOrdinal"/> since they are not valid C#
    /// identifiers and cannot be accessed as members.
    /// </summary>
    private static ProjectDetailsForGISDto MapToDto(SqlDataReader reader)
    {
        int ordId = reader.GetOrdinal("ID");
        int ordProjectName = reader.GetOrdinal("ProjectName");
        int ordPromoterName = reader.GetOrdinal("PromoterName");
        int ordPlotNo = reader.GetOrdinal("PlotNo");
        int ordArea = reader.GetOrdinal("Area");
        int ordPhaseArea = reader.GetOrdinal("PhaseArea");
        int ordRera = reader.GetOrdinal("RERA Registration Number");
        int ordStatus = reader.GetOrdinal("Application Status");

        return new ProjectDetailsForGISDto
        {
            Id = reader.GetInt32(ordId),
            ProjectName = GetSafeString(reader, ordProjectName),
            PromoterName = GetSafeString(reader, ordPromoterName),
            PlotNo = GetSafeString(reader, ordPlotNo),
            Area = GetSafeDecimal(reader, ordArea),
            PhaseArea = GetSafeDecimal(reader, ordPhaseArea),
            ReraRegistrationNumber = GetSafeString(reader, ordRera),
            ApplicationStatus = GetSafeString(reader, ordStatus)
        };
    }

    private static string GetSafeString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static decimal? GetSafeDecimal(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}
