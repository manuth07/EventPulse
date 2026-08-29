using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly ILoginService _loginService;

    public AuthController(
        IRegistrationService registrationService,
        ILoginService loginService)
    {
        _registrationService = registrationService;
        _loginService = loginService;
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

            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var result = await _registrationService.RegisterCustomerAsync(request);

        if (result.IsDuplicateEmail)
        {
            return Conflict(new { code = "DUPLICATE_EMAIL", message = "An account with this email already exists." });
        }

        if (!result.Succeeded)
        {
            return BadRequest(new { code = "REGISTRATION_FAILED", message = "Registration failed.", errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    /// <summary>
    /// POST /api/auth/login
    /// Public endpoint — authenticates user via email and password, returning JWT access token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var result = await _loginService.LoginAsync(request);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// POST /api/auth/verify-email
    /// Public endpoint — verifies user email address with 6-digit OTP.
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request, 
        [FromServices] Microsoft.AspNetCore.Identity.UserManager<Models.ApplicationUser> userManager,
        [FromServices] IEmailVerificationService verificationService)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "Invalid request." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { message = "Email is already verified." });
        }

        var result = await verificationService.VerifyCodeAsync(user.Id, request.Code);
        if (!result.Succeeded)
        {
            if (result.IsRateLimited) 
                return StatusCode(StatusCodes.Status429TooManyRequests, new { code = "RATE_LIMITED", message = result.ErrorMessage });
            
            return BadRequest(new { code = "VERIFICATION_FAILED", message = result.ErrorMessage });
        }

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        
        if (!updateResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "SERVER_ERROR", message = "Failed to update user status." });
        }

        return Ok(new { message = "Email successfully verified." });
    }

    /// <summary>
    /// POST /api/auth/resend-verification
    /// Public endpoint — resends email verification OTP code.
    /// </summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        [FromServices] Microsoft.AspNetCore.Identity.UserManager<Models.ApplicationUser> userManager,
        [FromServices] IEmailVerificationService verificationService)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Ok(new { message = "If an unverified account exists for this email, a verification code has been sent." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { message = "Account is already verified." });
        }

        var sent = await verificationService.SendVerificationCodeAsync(user.Id, user.Email!);
        if (!sent)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { code = "RATE_LIMITED", message = "Please wait before requesting another code." });
        }

        return Ok(new { message = "If an unverified account exists for this email, a verification code has been sent." });
    }

    /// <summary>
    /// GET /api/auth/me
    /// Protected endpoint — returns claims of the authenticated user (JWT verification test).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value;
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                        .Select(c => c.Value)
                        .Distinct()
                        .ToList();

        return Ok(new
        {
            userId,
            email,
            roles
        });
    }
}
