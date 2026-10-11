using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;
using Xunit;

namespace EventPulse.IdentityService.Tests;

/// <summary>
/// Unit and integration test suite for EP-369: Admin Management Role-Based Access Control.
/// 
/// Validates:
/// 1. Admin accesses protected endpoint successfully.
/// 2. Customer receives 403 Forbidden.
/// 3. Organizer receives 403 Forbidden.
/// 4. Anonymous (unauthenticated) request receives 401 Unauthorized.
/// 5. Invalid JWT (wrong signing key / malformed) is rejected with 401.
/// 6. Expired JWT is rejected with 401.
/// 7. Forged role claim is detected and rejected via live Identity verification.
/// 8. Admin role removed or account deactivated after login (stale JWT) is rejected.
/// 9. Existing Customer and Organizer functionality remains operational.
/// </summary>
public class AdminAuthorizationTests
{
    private const string TestSigningKey = "EventPulseTestSigningKey_2026_Secure32ByteString!";
    private const string TestIssuer = "EventPulse.IdentityService";
    private const string TestAudience = "EventPulse.Clients";

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<AdminOnlyAuthorizationHandler>> _handlerLoggerMock;
    private readonly AdminOnlyAuthorizationHandler _handler;
    private readonly AdminOnlyRequirement _requirement;

    public AdminAuthorizationTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _handlerLoggerMock = new Mock<ILogger<AdminOnlyAuthorizationHandler>>();
        _handler = new AdminOnlyAuthorizationHandler(_userManagerMock.Object, _handlerLoggerMock.Object);
        _requirement = new AdminOnlyRequirement();
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string? role, bool isAuthenticated = true)
    {
        if (!isAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, "user@eventpulse.lk")
        };

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }

    private static string GenerateJwt(
        Guid userId,
        string role,
        string signingKey = TestSigningKey,
        DateTime? expiresAt = null,
        string issuer = TestIssuer,
        string audience = TestAudience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, $"{role.ToLowerInvariant()}@eventpulse.lk"),
            new(ClaimTypes.Role, role)
        };

        var expires = expiresAt ?? DateTime.UtcNow.AddMinutes(30);
        var notBefore = expiresAt.HasValue ? expiresAt.Value.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(-1);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static ClaimsPrincipal ValidateJwt(string token, string validKey = TestSigningKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(validKey));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, parameters, out _);
    }

    // =========================================================================
    // 1. Admin accesses protected endpoint
    // =========================================================================

    [Fact]
    public async Task AdminAccessesProtectedEndpoint_WithActiveAdminAccount_Succeeds()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = adminId,
            Email = "admin@eventpulse.lk",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(adminId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsInRoleAsync(user, AppRoles.Administrator))
            .ReturnsAsync(true);

        var principal = CreatePrincipal(adminId, AppRoles.Administrator);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    // =========================================================================
    // 2. Customer receives 403 Forbidden
    // =========================================================================

    [Fact]
    public async Task CustomerAccessesProtectedEndpoint_ReturnsForbidden()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var principal = CreatePrincipal(customerId, AppRoles.Customer);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    // =========================================================================
    // 3. Organizer receives 403 Forbidden
    // =========================================================================

    [Fact]
    public async Task OrganizerAccessesProtectedEndpoint_ReturnsForbidden()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var principal = CreatePrincipal(organizerId, AppRoles.Organizer);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    // =========================================================================
    // 4. Anonymous request receives 401 Unauthorized
    // =========================================================================

    [Fact]
    public async Task AnonymousRequest_Unauthenticated_FailsAuthorization()
    {
        // Arrange
        var unauthenticatedPrincipal = CreatePrincipal(Guid.Empty, null, isAuthenticated: false);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, unauthenticatedPrincipal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    // =========================================================================
    // 5. Invalid JWT receives 401
    // =========================================================================

    [Fact]
    public void InvalidJwt_WrongSigningKey_FailsSignatureValidation()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var tamperedKey = "TamperedInvalidKey_ThatShouldFail256Bit!";
        var forgedToken = GenerateJwt(adminId, AppRoles.Administrator, signingKey: tamperedKey);

        // Act & Assert
        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            ValidateJwt(forgedToken, validKey: TestSigningKey);
        });
    }

    [Fact]
    public void MalformedJwt_FailsValidation()
    {
        // Arrange
        var malformedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.malformed.payload";

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
        {
            ValidateJwt(malformedToken);
        });
    }

    // =========================================================================
    // 6. Expired JWT receives 401
    // =========================================================================

    [Fact]
    public void ExpiredJwt_FailsValidation()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddMinutes(-10);
        var expiredToken = GenerateJwt(adminId, AppRoles.Administrator, expiresAt: pastDate);

        // Act & Assert
        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            ValidateJwt(expiredToken);
        });
    }

    // =========================================================================
    // 7. Forged role is rejected
    // =========================================================================

    [Fact]
    public async Task ForgedRole_WhenTokenClaimsAdmin_ButDatabaseUserIsNotAdmin_IsRejected()
    {
        // Arrange
        // Attacker somehow acquired a validly signed token bearing Administrator claim,
        // but the user record in Identity database only has Customer role.
        var userId = Guid.NewGuid();
        var customerUser = new ApplicationUser
        {
            Id = userId,
            Email = "attacker@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(customerUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(customerUser, AppRoles.Administrator))
            .ReturnsAsync(false); // Live database check rejects!

        var principal = CreatePrincipal(userId, AppRoles.Administrator);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    // =========================================================================
    // 8. Admin role removed or account deactivated after login (stale JWT defense)
    // =========================================================================

    [Fact]
    public async Task AdminRoleRemovedAfterLogin_StaleJwt_IsRejected()
    {
        // Arrange
        // Admin logged in and obtained a 60-min JWT. 5 minutes later, an administrator
        // demoted the user in the database. The presented JWT still contains 'Administrator'.
        var adminId = Guid.NewGuid();
        var demotedUser = new ApplicationUser
        {
            Id = adminId,
            Email = "former.admin@eventpulse.lk",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(adminId.ToString()))
            .ReturnsAsync(demotedUser);
        _userManagerMock.Setup(m => m.IsInRoleAsync(demotedUser, AppRoles.Administrator))
            .ReturnsAsync(false); // No longer an admin in DB

        var principal = CreatePrincipal(adminId, AppRoles.Administrator);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AdminAccountDeactivatedAfterLogin_StaleJwt_IsRejected()
    {
        // Arrange
        // Admin account was suspended (IsActive = false).
        var adminId = Guid.NewGuid();
        var deactivatedUser = new ApplicationUser
        {
            Id = adminId,
            Email = "suspended.admin@eventpulse.lk",
            IsActive = false // Account deactivated!
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(adminId.ToString()))
            .ReturnsAsync(deactivatedUser);

        var principal = CreatePrincipal(adminId, AppRoles.Administrator);
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        _userManagerMock.Verify(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    // =========================================================================
    // 9. Existing user functionality remains operational
    // =========================================================================

    [Fact]
    public void ExistingUserControllers_DoNotRequireAdminOnlyPolicy()
    {
        // Verify that standard UsersController requires only authentication ([Authorize]),
        // not the AdminOnly policy, preserving full customer profile accessibility.
        var usersControllerType = typeof(UsersController);
        var authorizeAttrs = usersControllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(authorizeAttrs);
        // Does not restrict normal users with AdminOnly policy
        Assert.True(string.IsNullOrEmpty(authorizeAttrs.First().Policy) || authorizeAttrs.First().Policy != AppPolicies.AdminOnly);
    }

    [Fact]
    public void OrganizerApplicationsController_AdminEndpoints_RequireAdminOnlyPolicy()
    {
        // Verify that administrative endpoints in OrganizerApplicationsController
        // are explicitly guarded by AdminOnly policy.
        var controllerType = typeof(OrganizerApplicationsController);
        var getAdminMethod = controllerType.GetMethod(nameof(OrganizerApplicationsController.GetAdminApplications));
        var approveMethod = controllerType.GetMethod(nameof(OrganizerApplicationsController.ApproveApplication));
        var rejectMethod = controllerType.GetMethod(nameof(OrganizerApplicationsController.RejectApplication));

        Assert.NotNull(getAdminMethod);
        Assert.NotNull(approveMethod);
        Assert.NotNull(rejectMethod);

        var getAttr = getAdminMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        var approveAttr = approveMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        var rejectAttr = rejectMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(getAttr);
        Assert.Equal(AppPolicies.AdminOnly, getAttr.Policy);

        Assert.NotNull(approveAttr);
        Assert.Equal(AppPolicies.AdminOnly, approveAttr.Policy);

        Assert.NotNull(rejectAttr);
        Assert.Equal(AppPolicies.AdminOnly, rejectAttr.Policy);
    }
}
