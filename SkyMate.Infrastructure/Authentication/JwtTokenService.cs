using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkyMate.Application.DTOs.Auth;
using SkyMate.Application.Interfaces.Authentication;

namespace SkyMate.Infrastructure.Authentication;

/// <summary>Creates signed JWT access tokens from a user's identity and roles.</summary>
/// <param name="options">The issuer, audience, signing key, and token lifetime.</param>
/// <param name="timeProvider">The clock used to set token timestamps.</param>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>Builds and signs a token for the user.</summary>
    /// <param name="userId">The user's numeric ID.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="roles">The user's assigned roles.</param>
    /// <returns>The signed token and its expiration details.</returns>
    public AccessTokenResult CreateAccessToken(int userId, string email, IReadOnlyCollection<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(_options.ExpiryMinutes);
        // The subject identifies the user, and role claims support role-based authorization.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc,
            checked(_options.ExpiryMinutes * 60));
    }
}
