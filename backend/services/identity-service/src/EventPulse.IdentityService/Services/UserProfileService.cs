using Microsoft.AspNetCore.Identity;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Services;

public class ProfileResult
{
    public bool Succeeded { get; private set; }
    public object? Response { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    public static ProfileResult Success(object response) => new() { Succeeded = true, Response = response, StatusCode = 200 };
    public static ProfileResult NotFound() => new() { Succeeded = false, Code = "NOT_FOUND", Message = "User not found.", StatusCode = 404 };
    public static ProfileResult BadRequest(string code, string message) => new() { Succeeded = false, Code = code, Message = message, StatusCode = 400 };
    public static ProfileResult Conflict(string code, string message) => new() { Succeeded = false, Code = code, Message = message, StatusCode = 409 };
    public static ProfileResult ServerError() => new() { Succeeded = false, Code = "SERVER_ERROR", Message = "Profile update failed. Please try again.", StatusCode = 500 };
}

public interface IUserProfileService
{
    Task<ProfileResult> GetProfileAsync(Guid userId);
    Task<ProfileResult> CompleteProfileAsync(Guid userId, CompleteProfileRequest request);
    Task<ProfileResult> UpdateEmailAsync(Guid userId, string newEmail);
    Task<ProfileResult> UpdatePhoneAsync(Guid userId, string newPhoneNumber);
}

/// <summary>
/// Handles completing a Google-created account's profile (phone + country).
/// Derives the user from the JWT — never from a request body userId.
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(UserManager<ApplicationUser> userManager, ILogger<UserProfileService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ProfileResult> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("GetProfile: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.Contains(AppRoles.Administrator)
            ? AppRoles.Administrator
            : roles.Contains(AppRoles.Organizer)
            ? AppRoles.Organizer
            : AppRoles.Customer;

        var hasPassword = await _userManager.HasPasswordAsync(user);

        var profile = new UserProfileResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            CountryCode = user.CountryCode,
            ProfileCompleted = user.ProfileCompleted,
            HasPassword = hasPassword,
            Role = primaryRole,
            Roles = roles.ToList(),
            ProfilePictureUrl = null, // Handled in Phase 3
            CreatedAt = user.CreatedAt
        };

        return ProfileResult.Success(profile);
    }

    public async Task<ProfileResult> CompleteProfileAsync(Guid userId, CompleteProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogError("CompleteProfile: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        user.PhoneNumber = request.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
        user.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        user.ProfileCompleted = true;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to update profile for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return ProfileResult.ServerError();
        }

        _logger.LogInformation("Profile completion succeeded for user {UserId}.", userId);

        return ProfileResult.Success(new
        {
            id = user.Id,
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            phoneNumber = user.PhoneNumber,
            countryCode = user.CountryCode,
            profileCompleted = user.ProfileCompleted
        });
    }

    public async Task<ProfileResult> UpdateEmailAsync(Guid userId, string newEmail)
    {
        var normalizedNewEmail = newEmail.Trim();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("UpdateEmail: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        // If user already has this email, return success without change
        if (string.Equals(user.Email, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileResult.Success(new
            {
                email = user.Email,
                message = "Email address is already up to date."
            });
        }

        // Check for duplicate account with this email
        var existingWithEmail = await _userManager.FindByEmailAsync(normalizedNewEmail);
        if (existingWithEmail != null && existingWithEmail.Id != user.Id)
        {
            _logger.LogWarning("UpdateEmail: duplicate email {Email} attempted by user {UserId}.", normalizedNewEmail, userId);
            return ProfileResult.Conflict("DUPLICATE_EMAIL", "An account with this email address already exists.");
        }

        user.Email = normalizedNewEmail;
        user.UserName = normalizedNewEmail;
        user.NormalizedEmail = _userManager.NormalizeEmail(normalizedNewEmail);
        user.NormalizedUserName = _userManager.NormalizeName(normalizedNewEmail);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("UpdateEmail: failed for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return ProfileResult.ServerError();
        }

        _logger.LogInformation("UpdateEmail: successfully updated email for user {UserId} to {Email}.", userId, normalizedNewEmail);

        return ProfileResult.Success(new
        {
            email = user.Email,
            message = "Email address updated successfully."
        });
    }

    public async Task<ProfileResult> UpdatePhoneAsync(Guid userId, string newPhoneNumber)
    {
        var sanitizedPhone = newPhoneNumber.Trim().Replace(" ", "").Replace("-", "");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("UpdatePhone: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        user.PhoneNumber = sanitizedPhone;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("UpdatePhone: failed for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return ProfileResult.ServerError();
        }

        _logger.LogInformation("UpdatePhone: successfully updated phone for user {UserId}.", userId);

        return ProfileResult.Success(new
        {
            phoneNumber = user.PhoneNumber,
            message = "Phone number updated successfully."
        });
    }
}
