namespace SkyMate.API.DTOs.Auth;

/// <summary>Public account details returned after successful registration.</summary>
public sealed record RegisterResponse(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles);
