namespace SkyMate.Application.DTOs.Auth;

/// <summary>Contains a signed access token and its expiration details.</summary>
/// <param name="AccessToken">The signed JWT sent to the client.</param>
/// <param name="ExpiresAtUtc">The token expiration time in UTC.</param>
/// <param name="ExpiresInSeconds">The token lifetime in seconds.</param>
public sealed record AccessTokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    int ExpiresInSeconds);
