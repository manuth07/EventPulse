using Microsoft.AspNetCore.Identity;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;

namespace EventPulse.IdentityService.Services;

public class LoginService : ILoginService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginService> _logger;

    public LoginService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ILogger<LoginService> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        // 1. Account Lookup (using normalized email behavior of Identity)
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login rejected: user not found for provided email.");
            return LoginResult.InvalidCredentials();
        }

        // 2. Password Verification (ASP.NET Core Identity password hasher)
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Login rejected: invalid password for user ID {UserId}.", user.Id);
            return LoginResult.InvalidCredentials();
        }

        // 3. Email Verification Check
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login rejected: email unconfirmed for user ID {UserId}.", user.Id);
            return LoginResult.EmailNotVerified();
        }

        // 4. Account Active Status Check
        if (!user.IsActive)
        {
            _logger.LogWarning("Login rejected: account disabled for user ID {UserId}.", user.Id);
            return LoginResult.AccountDisabled();
        }

        // 5. Role Retrieval
        var roles = await _userManager.GetRolesAsync(user);

        // 6. JWT Token Generation
        var (token, expiresIn) = _jwtTokenService.GenerateToken(user, roles);

        _logger.LogInformation("Login succeeded for user ID {UserId}.", user.Id);

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
                Roles = roles
            }
        };

        return LoginResult.Success(response);
    }
}
