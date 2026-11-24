using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SkyMate.API.Security;

/// <summary>Reads the authenticated user's numeric ID from request claims.</summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the JWT subject claim and parses it as an integer. This avoids trusting
    /// a user ID supplied by the client.
    /// </summary>
    /// <param name="principal">The authenticated request principal.</param>
    /// <returns>The user ID, or <see langword="null"/> when the claim is missing or invalid.</returns>
    public static int? GetAuthenticatedUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? userId
            : null;
    }
}
