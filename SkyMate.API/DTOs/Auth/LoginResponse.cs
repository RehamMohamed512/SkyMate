namespace SkyMate.API.DTOs.Auth;

/// <summary>Contains the access token, its expiration details, and the public user profile.</summary>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    int ExpiresInSeconds,
    AuthUserResponse User);
