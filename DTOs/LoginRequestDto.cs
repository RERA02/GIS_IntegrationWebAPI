using System.ComponentModel.DataAnnotations;

namespace ReraGIS.Api.DTOs;

/// <summary>
/// Credentials shape for <c>POST /api/Auth/login</c>.
/// </summary>
/// <remarks>
/// Currently unused: by explicit request, <c>AuthController.Login</c> no longer accepts a request
/// body at all -- both the username and password are hardcoded server-side, so there is nothing
/// for the client to fill in on Swagger/Postman. This class is kept in the project (unreferenced)
/// so it's ready to use again once real <c>[user]</c>-table-backed login is wired up -- see the
/// README's "Replacing the demo login" section.
/// </remarks>
public class LoginRequestDto
{
    /// <summary>
    /// The user's login name.
    /// </summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The user's password (sent over HTTPS only; never logged).
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}
