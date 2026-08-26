using Microsoft.AspNetCore.Identity;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Handles the email/password customer registration workflow.
/// Uses UserManager for identity persistence and password hashing.
/// Never manually hashes passwords or interacts with the DB directly.
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger<RegistrationService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<RegistrationResult> RegisterCustomerAsync(RegisterRequest request)
    {
        // ------------------------------------------------------------------
        // 1. Normalize inputs
        // ------------------------------------------------------------------
        var email = request.Email.Trim().ToLowerInvariant();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var phoneNumber = NormalizePhone(request.PhoneNumber);
        var countryCode = request.CountryCode.Trim().ToUpperInvariant();

        // ------------------------------------------------------------------
        // 2. Duplicate email check (via normalized email — case insensitive)
        // ------------------------------------------------------------------
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return RegistrationResult.Duplicate();
        }

        // ------------------------------------------------------------------
        // 3. Build ApplicationUser — role is NOT accepted from client
        // ------------------------------------------------------------------
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email,           // Identity uses UserName as the unique login key
            PhoneNumber = phoneNumber,
            CountryCode = countryCode,
            EmailConfirmed = false,     // Phase 3 will confirm via OTP
            ProfileCompleted = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        // ------------------------------------------------------------------
        // 4. Create user — Identity performs password hashing internally
        // ------------------------------------------------------------------
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description);
            return RegistrationResult.Failure(errors);
        }

        // ------------------------------------------------------------------
        // 5. Ensure Customer role exists (idempotent)
        // ------------------------------------------------------------------
        if (!await _roleManager.RoleExistsAsync(AppRoles.Customer))
        {
            var createRoleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(AppRoles.Customer));
            if (!createRoleResult.Succeeded)
            {
                // User was created but role could not be assigned — roll back
                _logger.LogError("Failed to create Customer role. Rolling back user {UserId}.", user.Id);
                await _userManager.DeleteAsync(user);
                return RegistrationResult.Failure(["Account setup failed. Please try again."]);
            }
        }

        // ------------------------------------------------------------------
        // 6. Assign Customer role
        // ------------------------------------------------------------------
        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign Customer role to user {UserId}. Rolling back.", user.Id);
            await _userManager.DeleteAsync(user);
            return RegistrationResult.Failure(["Account setup failed. Please try again."]);
        }

        _logger.LogInformation("Customer registered successfully: {UserId} ({Email})", user.Id, email);

        return RegistrationResult.Success(new RegisterResponse
        {
            Id = user.Id,
            Email = user.Email!,
            EmailConfirmed = false,
            VerificationRequired = true,
        });
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Strips spaces and formatting from phone numbers.
    /// Full international validation is deferred to a later phase.
    /// </summary>
    private static string NormalizePhone(string raw)
        => raw.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
}
