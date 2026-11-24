using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SkyMate.UnitTests.Authentication;

/// <summary>Test-only endpoints for checking authentication and role authorization.</summary>
[ApiController]
[Route("test-auth")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AuthTestController : ControllerBase
{
    /// <summary>Returns success to any authenticated user.</summary>
    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected() => Ok(new { authenticated = true });

    /// <summary>Returns success only to users with the Admin role.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public IActionResult AdminOnly() => Ok(new { administrator = true });
}
