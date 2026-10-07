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
    /// GET /api/users/me
    /// Protected endpoint — returns the safe profile information for the authenticated user.
    /// User identity is derived strictly from the authenticated JWT claims.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token." });
        }

        var result = await _profileService.GetProfileAsync(userId);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
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

    /// <summary>
    /// PUT /api/users/me/email
    /// Protected endpoint — updates the authenticated user's email address.
    /// User identity is derived strictly from the authenticated JWT claims.
    /// </summary>
    [HttpPut("me/email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request)
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

        var result = await _profileService.UpdateEmailAsync(userId, request.NewEmail);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// PUT /api/users/me/phone
    /// Protected endpoint — updates the authenticated user's phone number.
    /// User identity is derived strictly from the authenticated JWT claims.
    /// </summary>
    [HttpPut("me/phone")]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest request)
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

        var result = await _profileService.UpdatePhoneAsync(userId, request.NewPhoneNumber);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// POST /api/users/me/change-password
    /// Protected endpoint — changes or adds password for authenticated user (EP-26 Phase 4).
    /// User identity is derived strictly from the authenticated JWT claims.
    /// </summary>
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
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

        var result = await _profileService.ChangePasswordAsync(userId, request);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// POST /api/users/me/avatar
    /// Protected endpoint — uploads/changes profile picture (EP-26 Phase 3).
    /// </summary>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "No image file provided." });
        }

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token." });
        }

        using var stream = file.OpenReadStream();
        var result = await _profileService.UpdateAvatarAsync(userId, stream, file.ContentType, file.FileName);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// DELETE /api/users/me/avatar
    /// Protected endpoint — removes profile picture (EP-26 Phase 3).
    /// </summary>
    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token." });
        }

        var result = await _profileService.RemoveAvatarAsync(userId);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }
}
