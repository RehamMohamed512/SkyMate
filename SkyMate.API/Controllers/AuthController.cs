using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkyMate.API.DTOs.Auth;
using SkyMate.Application.Interfaces.Authentication;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.API.Controllers;

/// <summary>Handles registration, sign-in, and current-user profile requests.</summary>
/// <param name="userManager">The Identity account and role manager.</param>
/// <param name="jwtTokenService">Creates access tokens for valid accounts.</param>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    private const string DefaultUserRole = "User";
    private const string InvalidCredentialsMessage = "The email address or password is incorrect.";

    /// <summary>Creates a new account with the default User role.</summary>
    /// <param name="request">The profile and password details submitted by the client.</param>
    /// <returns>Created account details, or a validation/conflict response.</returns>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim();
        // This gives a clear response; Identity and the database still enforce uniqueness.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Problem(
                title: "Email is already registered.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            LockoutEnabled = true
        };

        // UserManager stores a password hash and applies the configured password rules.
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Problem(
                    title: "Email is already registered.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return IdentityValidationProblem(createResult.Errors);
        }

        // Assign the role on the server so a public request cannot select Admin.
        var roleResult = await userManager.AddToRoleAsync(user, DefaultUserRole);
        if (!roleResult.Succeeded)
        {
            // Remove the partially created account if role assignment fails.
            var cleanupResult = await userManager.DeleteAsync(user);
            var errors = string.Join("; ", roleResult.Errors.Select(error => error.Code));
            if (!cleanupResult.Succeeded)
            {
                errors += "; user cleanup failed after role assignment failure";
            }

            throw new InvalidOperationException($"Could not assign the default User role. {errors}");
        }

        var roles = await userManager.GetRolesAsync(user);
        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.ToArray()));
    }




    /// <summary>Checks the submitted credentials and returns a signed access token.</summary>
    /// <param name="request">The email address and password.</param>
    /// <returns>The token and profile, or an authentication/validation error.</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Identity tracks failed attempts and applies the configured lockout period.
            if (await userManager.GetLockoutEnabledAsync(user))
            {
                await userManager.AccessFailedAsync(user);
            }

            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        // Include current roles so authorization policies can check them from the token.
        var roles = await userManager.GetRolesAsync(user);
        var token = jwtTokenService.CreateAccessToken(user.Id, user.Email!, roles.ToArray());
        var profile = ToAuthUserResponse(user, roles.ToArray());

        return Ok(new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            token.ExpiresInSeconds,
            profile));
    }




    /// <summary>Returns the profile for the authenticated user.</summary>
    /// <returns>The user's public profile, or Unauthorized when the identity is invalid.</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        // The validated Bearer token supplies the subject; the client cannot choose this ID.
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString(CultureInfo.InvariantCulture));
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(ToAuthUserResponse(user, roles.ToArray()));
    }




    private IActionResult InvalidCredentials() => Problem(
        title: InvalidCredentialsMessage,
        statusCode: StatusCodes.Status401Unauthorized);

    private IActionResult IdentityValidationProblem(IEnumerable<IdentityError> identityErrors)
    {
        // Group password errors separately from other account validation errors.
        var errors = identityErrors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());

        return BadRequest(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        });
    }

    private static AuthUserResponse ToAuthUserResponse(
        ApplicationUser user,
        IReadOnlyCollection<string> roles) => new(
        user.Id,
        user.Email!,
        user.FirstName,
        user.LastName,
        roles);
}
