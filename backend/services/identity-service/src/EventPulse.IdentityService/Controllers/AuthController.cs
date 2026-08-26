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

    /// <summary>
    /// POST /api/auth/verify-email
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, 
        [FromServices] Microsoft.AspNetCore.Identity.UserManager<Models.ApplicationUser> userManager,
        [FromServices] IEmailVerificationService verificationService)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Validation failed.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid request." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { message = "Email is already verified." });
        }

        var result = await verificationService.VerifyCodeAsync(user.Id, request.Code);
        if (!result.Succeeded)
        {
            if (result.IsRateLimited) return StatusCode(StatusCodes.Status429TooManyRequests, new { message = result.ErrorMessage });
            return BadRequest(new { message = result.ErrorMessage });
        }

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        
        if (!updateResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update user status." });
        }

        return Ok(new { message = "Email successfully verified." });
    }

    /// <summary>
    /// POST /api/auth/resend-verification
    /// </summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request,
        [FromServices] Microsoft.AspNetCore.Identity.UserManager<Models.ApplicationUser> userManager,
        [FromServices] IEmailVerificationService verificationService)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Validation failed.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Do not reveal if user exists or not
            return Ok(new { message = "If an unverified account exists for this email, a verification code has been sent." });
        }

        if (user.EmailConfirmed)
        {
            // Do not reveal if user exists or not, just return a generic success-looking response or specific if preferred. 
            // The prompt says: "If an already-confirmed user calls verify/resend: handle it cleanly. Do not send endless unnecessary OTP emails. Return an appropriate safe response."
            return Ok(new { message = "Account is already verified." });
        }

        var sent = await verificationService.SendVerificationCodeAsync(user.Id, user.Email);
        if (!sent)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Please wait before requesting another code." });
        }

        return Ok(new { message = "If an unverified account exists for this email, a verification code has been sent." });
    }
}
