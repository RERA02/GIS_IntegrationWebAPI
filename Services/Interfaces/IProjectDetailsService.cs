using ReraGIS.Api.DTOs;

namespace ReraGIS.Api.Services.Interfaces;

/// <summary>
/// Outcome of a <see cref="IProjectDetailsService.GetProjectDetailsForGISAsync"/> call.
/// </summary>
public enum ProjectDetailsQueryStatus
{
    /// <summary>No row was returned for the given project ID.</summary>
    NotFound,

    /// <summary>Exactly one row was returned, as expected.</summary>
    Single,

    /// <summary>
    /// More than one row was returned for the given project ID. This should not normally happen
    /// (the stored procedure filters on the primary key), but is handled defensively rather than
    /// silently discarding rows or throwing.
    /// </summary>
    MultipleRowsFound
}

/// <summary>
/// Wraps the result of executing <c>Get_ProjectDetailsForGIS</c> together with a status describing
/// how many rows were found, so the controller can decide how to shape the HTTP response.
/// </summary>
public sealed class ProjectDetailsQueryResult
{
    public ProjectDetailsQueryStatus Status { get; private init; }

    public ProjectDetailsForGISDto? Data { get; private init; }

    public static ProjectDetailsQueryResult NotFoundResult() =>
        new() { Status = ProjectDetailsQueryStatus.NotFound };

    public static ProjectDetailsQueryResult SingleResult(ProjectDetailsForGISDto dto) =>
        new() { Status = ProjectDetailsQueryStatus.Single, Data = dto };

    public static ProjectDetailsQueryResult MultipleRowsResult() =>
        new() { Status = ProjectDetailsQueryStatus.MultipleRowsFound };
}

/// <summary>
/// Executes the <c>Get_ProjectDetailsForGIS</c> stored procedure and maps its result to
/// <see cref="ProjectDetailsForGISDto"/>.
/// </summary>
public interface IProjectDetailsService
{
 
    Task<ProjectDetailsQueryResult> GetProjectDetailsForGISAsync(
        int projectId,
        int currentLogInUserId,
        CancellationToken cancellationToken);
}
