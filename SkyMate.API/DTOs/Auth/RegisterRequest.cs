using System.ComponentModel.DataAnnotations;

namespace SkyMate.API.DTOs.Auth;

/// <summary>Profile and credential fields submitted when creating an account.</summary>
public sealed class RegisterRequest
{
    /// <summary>The required first name, up to 100 characters.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>The required last name, up to 100 characters.</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>The password. ASP.NET Identity applies the remaining password rules.</summary>
    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    /// <summary>Must match <see cref="Password"/>.</summary>
    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;
}
