using SkyMate.Application.DTOs.Auth;

namespace SkyMate.Application.Interfaces.Authentication;

/// <summary>Defines how the application requests signed JWT access tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates an access token with the user's identity and roles.</summary>
    /// <param name="userId">The user's Identity ID.</param>
    /// <param name="email">The email claim added to the token.</param>
    /// <param name="roles">The role claims added to the token.</param>
    /// <returns>The signed token and its expiration details.</returns>
    AccessTokenResult CreateAccessToken(int userId, string email, IReadOnlyCollection<string> roles);
}
