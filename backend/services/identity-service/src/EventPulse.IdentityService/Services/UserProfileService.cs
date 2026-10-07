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
    Task<ProfileResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ProfileResult> UpdateAvatarAsync(Guid userId, Stream imageStream, string contentType, string fileName);
    Task<ProfileResult> RemoveAvatarAsync(Guid userId);
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
            ProfilePictureUrl = user.ProfilePictureUrl,
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

    public async Task<ProfileResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ProfileResult.BadRequest("PASSWORD_MISMATCH", "Passwords do not match.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("ChangePassword: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        var hasExistingPassword = await _userManager.HasPasswordAsync(user);

        IdentityResult result;
        if (hasExistingPassword)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return ProfileResult.BadRequest("CURRENT_PASSWORD_REQUIRED", "Current password is required.");
            }

            result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        }
        else
        {
            // Google-only account adding a password for the first time
            result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        }

        if (!result.Succeeded)
        {
            var isCurrentPasswordIncorrect = result.Errors.Any(e =>
                e.Code.Contains("PasswordMismatch", StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains("incorrect password", StringComparison.OrdinalIgnoreCase));

            if (isCurrentPasswordIncorrect)
            {
                _logger.LogWarning("ChangePassword: incorrect current password for user {UserId}.", userId);
                return ProfileResult.BadRequest("INCORRECT_CURRENT_PASSWORD", "The current password provided is incorrect.");
            }

            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("ChangePassword: policy violation for user {UserId}: {Errors}", userId, errors);
            return ProfileResult.BadRequest("PASSWORD_POLICY_VIOLATION", errors);
        }

        _logger.LogInformation("ChangePassword: password successfully updated for user {UserId}.", userId);

        return ProfileResult.Success(new
        {
            message = "Password changed successfully."
        });
    }

    public async Task<ProfileResult> UpdateAvatarAsync(Guid userId, Stream imageStream, string contentType, string fileName)
    {
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedTypes.Contains(contentType))
        {
            return ProfileResult.BadRequest("INVALID_IMAGE_TYPE", "Only JPEG, PNG, and WebP images are allowed.");
        }

        const long maxSizeBytes = 2 * 1024 * 1024; // 2MB
        if (imageStream.Length > maxSizeBytes)
        {
            return ProfileResult.BadRequest("FILE_TOO_LARGE", "Profile image size cannot exceed 2 MB.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("UpdateAvatar: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        // Determine extension
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        // Save file locally in wwwroot/avatars
        var avatarsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
        if (!Directory.Exists(avatarsDir))
        {
            Directory.CreateDirectory(avatarsDir);
        }

        var avatarFileName = $"{userId}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(avatarsDir, avatarFileName);

        imageStream.Position = 0;
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await imageStream.CopyToAsync(fileStream);
        }

        // Clean up old local avatar if it exists
        if (!string.IsNullOrWhiteSpace(user.ProfilePictureUrl) && user.ProfilePictureUrl.StartsWith("/avatars/"))
        {
            var oldFileName = Path.GetFileName(user.ProfilePictureUrl);
            var oldFilePath = Path.Combine(avatarsDir, oldFileName);
            if (File.Exists(oldFilePath))
            {
                try { File.Delete(oldFilePath); } catch { /* best effort */ }
            }
        }

        user.ProfilePictureUrl = $"/avatars/{avatarFileName}";
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogError("UpdateAvatar: failed to update user record {UserId}", userId);
            return ProfileResult.ServerError();
        }

        _logger.LogInformation("UpdateAvatar: avatar successfully updated for user {UserId}.", userId);

        return ProfileResult.Success(new
        {
            profilePictureUrl = user.ProfilePictureUrl,
            message = "Profile photo updated successfully."
        });
    }

    public async Task<ProfileResult> RemoveAvatarAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("RemoveAvatar: user {UserId} not found.", userId);
            return ProfileResult.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(user.ProfilePictureUrl) && user.ProfilePictureUrl.StartsWith("/avatars/"))
        {
            var avatarsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
            var oldFileName = Path.GetFileName(user.ProfilePictureUrl);
            var oldFilePath = Path.Combine(avatarsDir, oldFileName);
            if (File.Exists(oldFilePath))
            {
                try { File.Delete(oldFilePath); } catch { /* best effort */ }
            }
        }

        user.ProfilePictureUrl = null;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogError("RemoveAvatar: failed to update user record {UserId}", userId);
            return ProfileResult.ServerError();
        }

        _logger.LogInformation("RemoveAvatar: avatar removed for user {UserId}.", userId);

        return ProfileResult.Success(new
        {
            profilePictureUrl = (string?)null,
            message = "Profile photo removed successfully."
        });
    }
}
