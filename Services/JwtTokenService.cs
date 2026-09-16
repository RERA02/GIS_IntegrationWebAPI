using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ReraGIS.Api.Services;

/// <summary>
/// Result of issuing a JWT: the encoded token plus its UTC expiry.
/// </summary>
public readonly record struct GeneratedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Creates signed JWTs for authenticated users. Only the demo login endpoint (<c>AuthController</c>)
/// calls this in this sample; in a real system, any successful credential check would call it instead.
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Builds a signed JWT containing <c>sub</c>, <see cref="ClaimTypes.NameIdentifier"/> and
    /// <c>userId</c> claims (all three, per the API's claim-reading contract), plus the user name
    /// and a unique token ID.
    /// </summary>
    public GeneratedToken GenerateToken(int userId, string userName)
    {
        IConfigurationSection jwtSection = _configuration.GetSection("Jwt");

        string key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        string? issuer = jwtSection["Issuer"];
        string? audience = jwtSection["Audience"];
        int expiryMinutes = jwtSection.GetValue<int?>("ExpiryMinutes") ?? 60;

        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            // HMAC-SHA256 signing keys should be at least 256 bits (32 bytes); a shorter key
            // is a common misconfiguration that silently weakens the token's signature.
            throw new InvalidOperationException(
                "Jwt:Key must be at least 32 characters long for HMAC-SHA256 signing.");
        }

        DateTime nowUtc = DateTime.UtcNow;
        DateTime expiresAtUtc = nowUtc.AddMinutes(expiryMinutes);
        string userIdText = userId.ToString(CultureInfo.InvariantCulture);

        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, userIdText),
            new Claim(ClaimTypes.NameIdentifier, userIdText),
            new Claim("userId", userIdText),
            new Claim(JwtRegisteredClaimNames.UniqueName, userName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(nowUtc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        ];

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(key));
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new GeneratedToken(tokenString, expiresAtUtc);
    }
}
