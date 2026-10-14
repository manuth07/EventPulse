using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Security;

/// <summary>
/// Centralized authorization handler for the AdminOnly policy.
/// 
/// Security Guarantees:
/// 1. Enforces authentication (unauthenticated requests are denied).
/// 2. Enforces presence of the server-validated Administrator role claim.
/// 3. Performs live database verification against Identity storage:
///    - Confirms the user exists.
///    - Confirms the account is currently active (IsActive == true).
///    - Confirms the user currently retains the Administrator role.
/// 
/// Defense Against Stale JWTs:
/// In standard JWT setups, a demoted or deactivated administrator could continue
/// accessing protected administrative actions until token expiration. This handler
/// eliminates stale-claim vulnerability by verifying active role status in real time.
/// </summary>
public class AdminOnlyAuthorizationHandler : AuthorizationHandler<AdminOnlyRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminOnlyAuthorizationHandler> _logger;

    public AdminOnlyAuthorizationHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<AdminOnlyAuthorizationHandler> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOnlyRequirement requirement)
    {
        // 1. Must be authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Admin authorization failed: Caller is not authenticated.");
            return;
        }

        // 2. Must possess the Administrator role claim from the cryptographically verified token
        var hasAdminRoleClaim = context.User.IsInRole(AppRoles.Administrator)
            || context.User.HasClaim(ClaimTypes.Role, AppRoles.Administrator)
            || context.User.HasClaim("role", AppRoles.Administrator);

        if (!hasAdminRoleClaim)
        {
            _logger.LogWarning("Admin authorization failed: Caller does not possess the Administrator role claim.");
            return;
        }

        // 3. Extract caller user ID from standard claims (NameIdentifier or sub)
        var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            _logger.LogWarning("Admin authorization failed: Caller token lacks a valid Guid NameIdentifier/sub claim.");
            return;
        }

        // 4. Live identity verification against Identity database
        var userManager = GetUserManager();
        if (userManager != null)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Admin authorization failed: User {UserId} does not exist in the database.", userId);
                return;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Admin authorization failed: User {UserId} account is deactivated.", userId);
                return;
            }

            var isStillAdmin = await userManager.IsInRoleAsync(user, AppRoles.Administrator);
            if (!isStillAdmin)
            {
                _logger.LogWarning("Admin authorization failed: Administrator role has been revoked from User {UserId}.", userId);
                return;
            }
        }

        // All authorization criteria satisfied
        _logger.LogDebug("Admin authorization succeeded for active Administrator {UserId}.", userId);
        context.Succeed(requirement);
    }

    private UserManager<ApplicationUser> GetUserManager()
    {
        return _userManager;
    }
}
