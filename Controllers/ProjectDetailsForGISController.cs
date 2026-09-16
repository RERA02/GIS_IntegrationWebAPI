using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReraGIS.Api.DTOs;
using ReraGIS.Api.Services.Interfaces;

namespace ReraGIS.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public class ProjectDetailsForGISController : ControllerBase
{
    private readonly IProjectDetailsService _projectDetailsService;
    private readonly ILogger<ProjectDetailsForGISController> _logger;
    private readonly string _returnUrl;

    public ProjectDetailsForGISController(
        IProjectDetailsService projectDetailsService,
        ILogger<ProjectDetailsForGISController> logger,
        IConfiguration configuration)
    {
        _projectDetailsService = projectDetailsService;
        _logger = logger;

        // Not read from the stored procedure -- see the ReturnURL property's XML doc comment
        // on ProjectDetailsForGISDto. Falls back to an empty string if unconfigured, rather than
        // throwing, so a missing setting doesn't take the whole endpoint down.
        _returnUrl = configuration["AppUrls:ReturnUrl"] ?? string.Empty;
    }

    [HttpGet("{projectId:int}/{currentLogInUserId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectDetailsForGISDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProjectDetailsForGIS(
        int projectId,
        int currentLogInUserId,
        CancellationToken cancellationToken)
    {
        if (projectId <= 0)
        {
            return BadRequest(ApiResponse<object>.FailResponse("Invalid project ID."));
        }

        if (currentLogInUserId <= 0)
        {
            return BadRequest(ApiResponse<object>.FailResponse("Invalid current login user ID."));
        }

        ProjectDetailsQueryResult result = await _projectDetailsService.GetProjectDetailsForGISAsync(
            projectId,
            currentLogInUserId,
            cancellationToken);

        return result.Status switch
        {
            ProjectDetailsQueryStatus.NotFound =>
                NotFound(ApiResponse<object>.FailResponse("Project not found.")),

            // See ProjectDetailsQueryStatus.MultipleRowsFound: an unexpected multi-row result is
            // reported as a successful-but-empty array rather than a single (ambiguous) object.
            ProjectDetailsQueryStatus.MultipleRowsFound =>
                Ok(ApiResponse<IEnumerable<ProjectDetailsForGISDto>>.SuccessResponse(
                    Array.Empty<ProjectDetailsForGISDto>(),
                    "Project details retrieved successfully.")),

            _ => Ok(ApiResponse<ProjectDetailsForGISDto>.SuccessResponse(
                    WithReturnUrl(result.Data!),
                    "Project details retrieved successfully."))
        };
    }

    /// <summary>Stamps the configured return URL onto a DTO before it goes out in the response.</summary>
    private ProjectDetailsForGISDto WithReturnUrl(ProjectDetailsForGISDto dto)
    {
        dto.ReturnURL = _returnUrl;
        return dto;
    }
}
