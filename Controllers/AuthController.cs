using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReraGIS.Api.DTOs;
using ReraGIS.Api.Services;

namespace ReraGIS.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    // ─────────────────────────────────────────────────────────────────────────────────────────
    // DEMO ONLY. By explicit request, this endpoint no longer accepts a username/password from
    // the caller at all -- there is no request body, and Swagger/Postman show no input fields for
    // it. The credentials below are fixed server-side and a token for this user is issued on every
    // call. This exists purely so the API is runnable and testable out of the box; it must be
    // replaced with a real lookup against the [user] table (which *does* need to accept a
    // username/password again) before this API is used for anything real. See the "Replacing the
    // demo login" section of the project README for exact guidance and sample code.
    // ─────────────────────────────────────────────────────────────────────────────────────────
    private const string DemoUserName = "admin";
    private const int DemoUserId = 1;

    private readonly JwtTokenService _jwtTokenService;

    public AuthController(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

   
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    public IActionResult Login()
    {
        GeneratedToken generated = _jwtTokenService.GenerateToken(DemoUserId, DemoUserName);

        var response = new LoginResponseDto
        {
            Token = generated.Token,
            ExpiresAtUtc = generated.ExpiresAtUtc,
            UserName = DemoUserName,
            UserId = DemoUserId
        };

        return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response, "Login successful."));
    }
}
