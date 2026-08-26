using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public AuthController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>
    /// POST /api/auth/register
    /// Public endpoint — no JWT required.
    /// Creates a new Customer account via email/password.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { message = "Validation failed.", errors });
        }

        var result = await _registrationService.RegisterCustomerAsync(request);

        if (result.IsDuplicateEmail)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Registration failed.", errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }
}
