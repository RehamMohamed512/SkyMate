using System.ComponentModel.DataAnnotations;

namespace SkyMate.API.DTOs.Auth;

/// <summary>Credentials submitted to sign in.</summary>
public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
