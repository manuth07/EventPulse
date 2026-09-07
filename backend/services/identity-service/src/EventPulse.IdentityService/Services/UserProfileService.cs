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
    public static ProfileResult ServerError() => new() { Succeeded = false, Code = "SERVER_ERROR", Message = "Profile update failed. Please try again.", StatusCode = 500 };
}

public interface IUserProfileService
{
    Task<ProfileResult> CompleteProfileAsync(Guid userId, CompleteProfileRequest request);
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
}
