namespace ReraGIS.Api.DTOs;

/// <summary>
/// Projection of the result set returned by the <c>Get_ProjectDetailsForGIS</c> stored procedure.
/// </summary>
/// <remarks>
/// The stored procedure returns two columns with spaces in their aliases
/// (<c>RERA Registration Number</c> and <c>Application Status</c>), which are not valid C# identifiers.
/// They are mapped explicitly to <see cref="ReraRegistrationNumber"/> and <see cref="ApplicationStatus"/>
/// in <c>ProjectDetailsService</c>.
/// </remarks>
public class ProjectDetailsForGISDto
{
    public int Id { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string PromoterName { get; set; } = string.Empty;

    public string PlotNo { get; set; } = string.Empty;

    public decimal? Area { get; set; }

    public decimal? PhaseArea { get; set; }

    /// <summary>Maps SQL column alias "RERA Registration Number".</summary>
    public string ReraRegistrationNumber { get; set; } = string.Empty;

    /// <summary>Maps SQL column alias "Application Status".</summary>
    public string ApplicationStatus { get; set; } = string.Empty;

    /// <summary>
    /// URL to send the caller back to after they're done (e.g. the RERA portal). Not part of the
    /// stored procedure's result set -- populated by the controller from
    /// <c>AppUrls:ReturnUrl</c> in configuration. Serializes as <c>returnURL</c> in the JSON
    /// response (the default camelCase policy only lowercases the leading "R").
    /// </summary>
    public string ReturnURL { get; set; } = string.Empty;
}
