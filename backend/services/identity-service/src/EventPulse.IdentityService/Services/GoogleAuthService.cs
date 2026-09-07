using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Handles all Google Identity Services authentication flows:
/// - New Google user creation
/// - Returning Google user lookup
/// - Linking an existing password account to Google
/// 
/// Token validation is performed using Google.Apis.Auth (GoogleJsonWebSignature).
/// The Google credential is NEVER stored. It is used only for the duration of the request.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly GoogleSettings _googleSettings;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GoogleAuthService> _logger;

    private const string GoogleProvider = "Google";

    public GoogleAuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IJwtTokenService jwtTokenService,
        IOptions<GoogleSettings> googleSettings,
        ApplicationDbContext db,
        ILogger<GoogleAuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
        _googleSettings = googleSettings.Value;
        _db = db;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Public: Google Login
    // -----------------------------------------------------------------------

    /// <summary>
    /// Main Google login flow.
    /// Validates the Google ID token, then:
    /// A) Returns existing Google-linked account
    /// B) Creates a new account if the email does not exist
    /// C) Returns ACCOUNT_LINK_REQUIRED if an email-only account exists
    /// </summary>
    public async Task<GoogleAuthResult> GoogleLoginAsync(string googleCredential)
    {
        // 1. Validate the Google ID token — signature, issuer, expiry, audience
        GoogleJsonWebSignature.Payload? payload;
        try
        {
            payload = await ValidateGoogleCredentialAsync(googleCredential);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Google credential validation failed: {Message}", ex.Message);
            return GoogleAuthResult.AuthFailed();
        }

        if (payload == null)
            return GoogleAuthResult.AuthFailed();

        // 2. Require Google to confirm email_verified
        if (!payload.EmailVerified)
        {
            _logger.LogWarning("Google login rejected: email_verified is false for sub={Sub}", payload.Subject);
            return GoogleAuthResult.EmailNotVerified();
        }

        var googleSub = payload.Subject;   // permanent Google user identifier
        var googleEmail = payload.Email.Trim().ToLowerInvariant();

        // 3. SCENARIO A — Look up by Google sub first (returning Google user)
        var existingGoogleUser = await _userManager.FindByLoginAsync(GoogleProvider, googleSub);
        if (existingGoogleUser != null)
        {
            return await IssueJwtForExistingUser(existingGoogleUser, "Returning Google user");
        }

        // 4. SCENARIO B/C — Look up by normalized email
        var emailUser = await _userManager.FindByEmailAsync(googleEmail);

        if (emailUser == null)
        {
            // SCENARIO B — No account. Create fresh Google-based user.
            return await CreateGoogleUserAsync(payload);
        }

        // 5. Email account exists but no Google login linked
        // Check if a DIFFERENT Google sub is already linked to this account (Scenario E/F conflict)
        var existingLogins = await _userManager.GetLoginsAsync(emailUser);
        var googleLogin = existingLogins.FirstOrDefault(l => l.LoginProvider == GoogleProvider);
        if (googleLogin != null && googleLogin.ProviderKey != googleSub)
        {
            _logger.LogWarning(
                "Google sub conflict for user {UserId}: existing sub differs from new sub.",
                emailUser.Id);
            return GoogleAuthResult.ExternalLoginConflict();
        }

        // 6. SCENARIO C — Password/unverified account exists → require linking flow
        return GoogleAuthResult.AccountLinkRequired();
    }

    // -----------------------------------------------------------------------
    // Public: Link Existing Account
    // -----------------------------------------------------------------------

    /// <summary>
    /// Securely links an existing password-based EventPulse account to Google.
    /// Revalidates the Google credential, verifies the existing EventPulse password,
    /// and only then adds the Google external login.
    /// </summary>
    public async Task<GoogleAuthResult> LinkExistingAccountAsync(string googleCredential, string eventPulsePassword)
    {
        // 1. Re-validate Google credential (never trust state from a previous request)
        GoogleJsonWebSignature.Payload? payload;
        try
        {
            payload = await ValidateGoogleCredentialAsync(googleCredential);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Google credential validation failed during link: {Message}", ex.Message);
            return GoogleAuthResult.AuthFailed();
        }

        if (payload == null) return GoogleAuthResult.AuthFailed();

        if (!payload.EmailVerified)
            return GoogleAuthResult.EmailNotVerified();

        var googleSub = payload.Subject;
        var googleEmail = payload.Email.Trim().ToLowerInvariant();

        // 2. Find the existing EventPulse account by Google's verified email
        var user = await _userManager.FindByEmailAsync(googleEmail);
        if (user == null)
        {
            // No account to link to — treat as a new user
            _logger.LogInformation("Link requested but no existing account found for email. Creating new Google user.");
            return await CreateGoogleUserAsync(payload);
        }

        // 3. Account active check
        if (!user.IsActive)
        {
            _logger.LogWarning("Link rejected: account disabled for user {UserId}.", user.Id);
            return GoogleAuthResult.AccountDisabled();
        }

        // 4. Verify the Google sub is not already linked to another account (conflict guard)
        var alreadyLinkedUser = await _userManager.FindByLoginAsync(GoogleProvider, googleSub);
        if (alreadyLinkedUser != null && alreadyLinkedUser.Id != user.Id)
        {
            _logger.LogWarning("Google sub {Sub} is already linked to a different user.", googleSub);
            return GoogleAuthResult.ExternalLoginConflict();
        }

        // 5. Verify the supplied EventPulse password
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, eventPulsePassword);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Link rejected: invalid password for user {UserId}.", user.Id);
            return GoogleAuthResult.InvalidCredentials();
        }

        // 6. Add Google external login to this account
        var addLoginResult = await _userManager.AddLoginAsync(
            user,
            new UserLoginInfo(GoogleProvider, googleSub, GoogleProvider));

        if (!addLoginResult.Succeeded)
        {
            // If it already exists (duplicate idempotent case), that is fine
            var alreadyExists = addLoginResult.Errors.Any(e =>
                e.Code is "LoginAlreadyAssociated");

            if (!alreadyExists)
            {
                _logger.LogError("Failed to add Google login to user {UserId}: {Errors}",
                    user.Id, string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                return GoogleAuthResult.ServerError("Failed to link Google account. Please try again.");
            }
        }

        // 7. If the account was unverified, confirm email now (both credentials proven)
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("EmailConfirmed set to true during Google link for user {UserId}.", user.Id);
        }

        _logger.LogInformation("Google account linked successfully to user {UserId}.", user.Id);

        return await IssueJwtForExistingUser(user, "Google link completed");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates a Google ID token using GoogleJsonWebSignature.
    /// Enforces audience (client ID), issuer, signature, and expiry.
    /// </summary>
    private async Task<GoogleJsonWebSignature.Payload?> ValidateGoogleCredentialAsync(string credential)
    {
        if (string.IsNullOrWhiteSpace(_googleSettings.ClientId))
        {
            throw new InvalidOperationException("Google:ClientId is not configured. Set it via User Secrets or environment variables.");
        }

        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_googleSettings.ClientId]
        };

        return await GoogleJsonWebSignature.ValidateAsync(credential, validationSettings);
    }

    /// <summary>
    /// Creates a new ApplicationUser from a validated Google payload.
    /// Uses a DB transaction to atomically create user + assign role + add Google login.
    /// </summary>
    private async Task<GoogleAuthResult> CreateGoogleUserAsync(GoogleJsonWebSignature.Payload payload)
    {
        var email = payload.Email.Trim().ToLowerInvariant();
        var firstName = (payload.GivenName ?? "").Trim();
        var lastName = (payload.FamilyName ?? "").Trim();

        // Fallback if Google does not provide name parts
        if (string.IsNullOrEmpty(firstName)) firstName = email.Split('@')[0];
        if (string.IsNullOrEmpty(lastName)) lastName = "-";

        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email,
            EmailConfirmed = true,      // Google verified the email
            PhoneNumber = null,
            CountryCode = null,
            ProfileCompleted = false,   // Requires /complete-profile step
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            // PasswordHash remains null — Google-only account
        };

        // Use a DB transaction: create user + add role + add Google login atomically
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Step 1: Create user (no password)
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                // Could be a concurrent duplicate-email race condition caught by DB uniqueness
                var isDuplicate = createResult.Errors.Any(e =>
                    e.Code is "DuplicateUserName" or "DuplicateEmail");

                if (isDuplicate)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning("Concurrent Google user creation detected for email {Email}. Treating as link required.", email);
                    return GoogleAuthResult.AccountLinkRequired();
                }

                await tx.RollbackAsync();
                _logger.LogError("Failed to create Google user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return GoogleAuthResult.ServerError();
            }

            // Step 2: Ensure Customer role exists and assign it
            if (!await _roleManager.RoleExistsAsync(AppRoles.Customer))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(AppRoles.Customer));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Customer);
            if (!roleResult.Succeeded)
            {
                await tx.RollbackAsync();
                _logger.LogError("Failed to assign Customer role to new Google user {UserId}.", user.Id);
                return GoogleAuthResult.ServerError();
            }

            // Step 3: Add Google external login
            var loginInfo = new UserLoginInfo(GoogleProvider, payload.Subject, GoogleProvider);
            var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);
            if (!addLoginResult.Succeeded)
            {
                await tx.RollbackAsync();
                _logger.LogError("Failed to add Google login to new user {UserId}.", user.Id);
                return GoogleAuthResult.ServerError();
            }

            await tx.CommitAsync();
            _logger.LogInformation("New Google user created: {UserId} ({Email})", user.Id, email);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Exception during Google user creation for {Email}.", email);
            return GoogleAuthResult.ServerError();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return BuildJwtResult(user, roles);
    }

    /// <summary>
    /// Issues a JWT for an existing user after all account checks pass.
    /// </summary>
    private async Task<GoogleAuthResult> IssueJwtForExistingUser(ApplicationUser user, string logContext)
    {
        if (!user.IsActive)
        {
            _logger.LogWarning("{Context}: account disabled for user {UserId}.", logContext, user.Id);
            return GoogleAuthResult.AccountDisabled();
        }

        var roles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation("{Context}: JWT issued for user {UserId}.", logContext, user.Id);
        return BuildJwtResult(user, roles);
    }

    /// <summary>
    /// Builds the final GoogleAuthResult.Success carrying an EventPulse JWT.
    /// Reuses the existing IJwtTokenService — no separate Google JWT implementation.
    /// </summary>
    private GoogleAuthResult BuildJwtResult(ApplicationUser user, IList<string> roles)
    {
        var (token, expiresIn) = _jwtTokenService.GenerateToken(user, roles);

        var response = new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = new AuthenticatedUserResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Roles = roles,
                ProfileCompleted = user.ProfileCompleted,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
            }
        };

        return GoogleAuthResult.Success(response);
    }
}
