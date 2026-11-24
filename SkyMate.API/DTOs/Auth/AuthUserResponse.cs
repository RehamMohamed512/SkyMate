namespace SkyMate.API.DTOs.Auth;

/// <summary>Public user profile fields returned by registration and the current-user endpoint.</summary>
public sealed record AuthUserResponse(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles);
