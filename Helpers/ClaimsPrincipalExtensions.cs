using System.Globalization;
using System.Security.Claims;

namespace ReraGIS.Api.Helpers;

/// <summary>
/// Extracts the current user's numeric ID from a JWT-authenticated <see cref="ClaimsPrincipal"/>,
/// checking every common claim name different token issuers use for it.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Claim types checked, in priority order, for the current user's numeric ID.
    /// </summary>
    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "userId",
        "UserId"
    ];

    /// <summary>
    /// Attempts to read the authenticated user's ID from the token's claims.
    /// </summary>
    /// <param name="principal">The current <see cref="ClaimsPrincipal"/> (typically <c>HttpContext.User</c>).</param>
    /// <param name="userId">The parsed user ID, when found; otherwise 0.</param>
    /// <returns>
    /// <see langword="true"/> if a recognized claim was present and parsed as a positive integer;
    /// otherwise <see langword="false"/>, in which case callers should treat the request as unauthorized.
    /// </returns>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        foreach (var claimType in UserIdClaimTypes)
        {
            var claimValue = principal.FindFirst(claimType)?.Value;

            if (!string.IsNullOrWhiteSpace(claimValue) &&
                int.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                parsed > 0)
            {
                userId = parsed;
                return true;
            }
        }

        userId = 0;
        return false;
    }
}
