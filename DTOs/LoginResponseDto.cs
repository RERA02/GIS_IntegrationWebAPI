namespace ReraGIS.Api.DTOs;

/// <summary>
/// Returned by <c>POST /api/Auth/login</c> on success.
/// </summary>
public class LoginResponseDto
{
    /// <summary>
    /// The signed JWT access token. Send it as <c>Authorization: Bearer {token}</c> on subsequent requests.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp at which <see cref="Token"/> expires.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// The authenticated user's login name (informational only; not used for authorization).
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The authenticated user's numeric ID, as embedded in the token's claims.
    /// </summary>
    public int UserId { get; set; }
}
