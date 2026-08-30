using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public UsersController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// PUT /api/users/me/profile
    /// Protected endpoint — completes the profile for a Google-created user.
    /// Collects PhoneNumber and CountryCode, then sets ProfileCompleted = true.
    /// User identity is derived from the authenticated JWT, never from the request body.
    /// </summary>
    [HttpPut("me/profile")]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token." });
        }

        var result = await _profileService.CompleteProfileAsync(userId, request);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }
}
