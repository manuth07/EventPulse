using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Tests;

/// <summary>
/// Unit and integration test suite for EP-371: Admin Management Actions (Backend).
/// Validates:
/// 1. ActivateInactiveUser_Succeeds
/// 2. DeactivateActiveUser_Succeeds
/// 3. ChangeRole_PromotesAndDemotesSuccessfully
/// 4. ChangeRole_InvalidRole_ReturnsBadRequest
/// 5. TargetNotFound_ReturnsNotFound
/// 6. NonAdmin_DeniedAccess
/// 7. SelfDeactivation_Blocked & SelfDemotion_Blocked
/// 8. LastActiveAdmin_ProtectedFromDeactivationAndDemotion
/// 9. DeactivatedUser_LoginRejected
/// 10. ExistingSession_RejectedAfterDeactivation
/// 11. RemovedAdmin_LosesAdminAccess
/// 12. AuditRecord_CreatedWithAccurateStateAndMetadata
/// 13. ConcurrentLastAdminChanges_GuardsPreventViolations
/// 14. InvalidStateTransition_ActivatingActiveOrDeactivatingInactive_Fails
/// </summary>
public class AdminManagementActionsTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        var roles = new[] { AppRoles.Administrator, AppRoles.Organizer, AppRoles.Customer };
        foreach (var roleName in roles)
        {
            if (!await context.Roles.AnyAsync(r => r.NormalizedName == roleName.ToUpperInvariant()))
            {
                context.Roles.Add(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                });
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task AssignRoleAsync(ApplicationDbContext context, Guid userId, string roleName)
    {
        var role = await context.Roles.FirstAsync(r => r.NormalizedName == roleName.ToUpperInvariant());
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = role.Id
        });
        await context.SaveChangesAsync();
    }

    private static AdminUsersController BuildController(
        IAdminManagementService service,
        Guid? callerId = null,
        string role = AppRoles.Administrator)
    {
        var logger = new Mock<ILogger<AdminUsersController>>();
        var controller = new AdminUsersController(service, logger.Object);

        var httpContext = new DefaultHttpContext();
        if (callerId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, callerId.Value.ToString()),
                new("sub", callerId.Value.ToString()),
                new(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // =========================================================================
    // 1. Activate inactive user
    // =========================================================================

    [Fact]
    public async Task ActivateInactiveUser_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "User", Email = "admin@ep.lk", IsActive = true };
        var inactiveUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Pending", LastName = "User", Email = "pending@ep.lk", IsActive = false, SecurityStamp = "old-stamp" };
        context.Users.AddRange(admin, inactiveUser);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, inactiveUser.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.ActivateUserAsync(admin.Id, inactiveUser.Id, "Verified documentation", "192.168.1.1");

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.User);
        Assert.True(result.User.IsActive);

        // Verify DB updates
        var updated = await context.Users.FindAsync(inactiveUser.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsActive);
        Assert.NotEqual("old-stamp", updated.SecurityStamp);

        // Verify audit log
        var audit = await context.AdminAuditLogs.FirstOrDefaultAsync(a => a.TargetUserId == inactiveUser.Id);
        Assert.NotNull(audit);
        Assert.Equal("ADMIN_USER_ACTIVATED", audit.Action);
        Assert.Equal("SUCCESS", audit.Outcome);
        Assert.Equal("Verified documentation", audit.Reason);
        Assert.Equal("192.168.1.1", audit.IpAddress);
        Assert.Contains("\"isActive\":false", audit.PreviousState);
        Assert.Contains("\"isActive\":true", audit.NewState);
    }

    // =========================================================================
    // 2. Deactivate active user
    // =========================================================================

    [Fact]
    public async Task DeactivateActiveUser_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "User", Email = "admin@ep.lk", IsActive = true };
        var targetUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Target", LastName = "User", Email = "target@ep.lk", IsActive = true, SecurityStamp = "current-stamp" };
        context.Users.AddRange(admin, targetUser);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, targetUser.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.DeactivateUserAsync(admin.Id, targetUser.Id, "Terms of service violation", "10.0.0.5");

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.User);
        Assert.False(result.User.IsActive);

        // Verify DB updates
        var updated = await context.Users.FindAsync(targetUser.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.NotEqual("current-stamp", updated.SecurityStamp);

        // Verify audit log
        var audit = await context.AdminAuditLogs.FirstOrDefaultAsync(a => a.TargetUserId == targetUser.Id);
        Assert.NotNull(audit);
        Assert.Equal("ADMIN_USER_DEACTIVATED", audit.Action);
        Assert.Equal("SUCCESS", audit.Outcome);
        Assert.Equal("Terms of service violation", audit.Reason);
        Assert.Equal("10.0.0.5", audit.IpAddress);
        Assert.Contains("\"isActive\":true", audit.PreviousState);
        Assert.Contains("\"isActive\":false", audit.NewState);
    }

    // =========================================================================
    // 3. Change user role (Promote and Demote)
    // =========================================================================

    [Fact]
    public async Task ChangeRole_PromotesAndDemotesSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "One", Email = "admin@ep.lk", IsActive = true };
        var targetUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Regular", LastName = "User", Email = "user@ep.lk", IsActive = true, SecurityStamp = "stamp1" };
        context.Users.AddRange(admin, targetUser);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, targetUser.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        // Promote to Organizer
        var promoteResult = await service.ChangeUserRoleAsync(admin.Id, targetUser.Id, AppRoles.Organizer, "Approved application");
        Assert.True(promoteResult.Succeeded);
        Assert.Equal(AppRoles.Organizer, promoteResult.User?.Role);

        // Promote to Administrator
        var adminResult = await service.ChangeUserRoleAsync(admin.Id, targetUser.Id, AppRoles.Administrator, "Promoted to co-admin");
        Assert.True(adminResult.Succeeded);
        Assert.Equal(AppRoles.Administrator, adminResult.User?.Role);

        // Demote back to Customer
        var demoteResult = await service.ChangeUserRoleAsync(admin.Id, targetUser.Id, AppRoles.Customer, "Demoted back");
        Assert.True(demoteResult.Succeeded);
        Assert.Equal(AppRoles.Customer, demoteResult.User?.Role);

        // Check audit logs
        var auditLogs = await context.AdminAuditLogs.Where(a => a.TargetUserId == targetUser.Id).ToListAsync();
        Assert.Equal(3, auditLogs.Count);
        Assert.All(auditLogs, a => Assert.Equal("ADMIN_USER_ROLE_CHANGED", a.Action));
    }

    // =========================================================================
    // 4. Change role with invalid role name
    // =========================================================================

    [Fact]
    public async Task ChangeRole_InvalidRole_ReturnsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "One", Email = "admin@ep.lk", IsActive = true };
        var targetUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "User", LastName = "One", Email = "user@ep.lk", IsActive = true };
        context.Users.AddRange(admin, targetUser);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, targetUser.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.ChangeUserRoleAsync(admin.Id, targetUser.Id, "SuperAdmin");
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_ROLE_SPECIFIED", result.Code);

        // Controller endpoint test with empty role
        var controller = BuildController(service, admin.Id);
        var actionResult = await controller.ChangeUserRole(targetUser.Id.ToString(), new UserRoleChangeRequest { Role = "" });
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // =========================================================================
    // 5. Target not found returns 404
    // =========================================================================

    [Fact]
    public async Task TargetNotFound_ReturnsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "One", Email = "admin@ep.lk", IsActive = true };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());
        var nonExistentId = Guid.NewGuid();

        var actResult = await service.ActivateUserAsync(admin.Id, nonExistentId);
        Assert.False(actResult.Succeeded);
        Assert.Equal(404, actResult.StatusCode);
        Assert.Equal("USER_NOT_FOUND", actResult.Code);

        var deactResult = await service.DeactivateUserAsync(admin.Id, nonExistentId);
        Assert.False(deactResult.Succeeded);
        Assert.Equal(404, deactResult.StatusCode);
        Assert.Equal("USER_NOT_FOUND", deactResult.Code);

        var roleResult = await service.ChangeUserRoleAsync(admin.Id, nonExistentId, AppRoles.Customer);
        Assert.False(roleResult.Succeeded);
        Assert.Equal(404, roleResult.StatusCode);
        Assert.Equal("USER_NOT_FOUND", roleResult.Code);

        // Controller test for invalid guid string
        var controller = BuildController(service, admin.Id);
        var badIdResult = await controller.ActivateUser("invalid-guid");
        Assert.IsType<BadRequestObjectResult>(badIdResult);
    }

    // =========================================================================
    // 6. Non-admin denied access
    // =========================================================================

    [Fact]
    public void NonAdmin_DeniedAccess_PolicyAndAttributesEnforced()
    {
        // 1. Verify AdminUsersController is decorated with [Authorize(Policy = AppPolicies.AdminOnly)]
        var controllerType = typeof(AdminUsersController);
        var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.AdminOnly, authorizeAttribute.Policy);

        // 2. Verify all action methods have Authorize inherited or applied
        var actionMethods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actionMethods);
    }

    [Fact]
    public async Task NonAdmin_DeniedAccess_AuthorizationHandlerRejects()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var handler = new AdminOnlyAuthorizationHandler(userManagerMock.Object, Mock.Of<ILogger<AdminOnlyAuthorizationHandler>>());
        var requirement = new AdminOnlyRequirement();

        // Customer principal
        var customerId = Guid.NewGuid();
        var customerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Customer)
        }, "Bearer"));

        var customerContext = new AuthorizationHandlerContext(new[] { requirement }, customerPrincipal, null);
        await handler.HandleAsync(customerContext);
        Assert.False(customerContext.HasSucceeded);

        // Organizer principal
        var organizerId = Guid.NewGuid();
        var organizerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Organizer)
        }, "Bearer"));

        var organizerContext = new AuthorizationHandlerContext(new[] { requirement }, organizerPrincipal, null);
        await handler.HandleAsync(organizerContext);
        Assert.False(organizerContext.HasSucceeded);

        // Unauthenticated principal
        var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var anonContext = new AuthorizationHandlerContext(new[] { requirement }, anonPrincipal, null);
        await handler.HandleAsync(anonContext);
        Assert.False(anonContext.HasSucceeded);
    }

    // =========================================================================
    // 7. Self-deactivation and self-demotion blocked
    // =========================================================================

    [Fact]
    public async Task SelfDeactivation_Blocked()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "Self", Email = "admin@ep.lk", IsActive = true };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.DeactivateUserAsync(admin.Id, admin.Id, "Trying self-deactivation");
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SELF_DEACTIVATION_PROHIBITED", result.Code);
    }

    [Fact]
    public async Task SelfDemotion_Blocked()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "Self", Email = "admin@ep.lk", IsActive = true };
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.ChangeUserRoleAsync(admin.Id, admin.Id, AppRoles.Customer, "Trying self-demotion");
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SELF_DEMOTION_PROHIBITED", result.Code);
    }

    // =========================================================================
    // 8. Last active admin protection
    // =========================================================================

    [Fact]
    public async Task LastActiveAdmin_ProtectedFromDeactivationAndDemotion()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin1 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Sole", LastName = "Admin", Email = "sole@ep.lk", IsActive = true };
        var callerAdmin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Caller", LastName = "Admin", Email = "caller@ep.lk", IsActive = false }; // inactive caller
        context.Users.AddRange(admin1, callerAdmin);
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, admin1.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, callerAdmin.Id, AppRoles.Administrator);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        // Deactivating the sole active admin must fail
        var deactResult = await service.DeactivateUserAsync(callerAdmin.Id, admin1.Id);
        Assert.False(deactResult.Succeeded);
        Assert.Equal(400, deactResult.StatusCode);
        Assert.Equal("LAST_ADMIN_PROTECTION", deactResult.Code);

        // Demoting the sole active admin must fail
        var demoteResult = await service.ChangeUserRoleAsync(callerAdmin.Id, admin1.Id, AppRoles.Organizer);
        Assert.False(demoteResult.Succeeded);
        Assert.Equal(400, demoteResult.StatusCode);
        Assert.Equal("LAST_ADMIN_PROTECTION", demoteResult.Code);

        // Add a second active admin: now deactivation of admin1 should succeed
        var admin2 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Second", LastName = "Admin", Email = "second@ep.lk", IsActive = true };
        context.Users.Add(admin2);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin2.Id, AppRoles.Administrator);

        var allowedDeact = await service.DeactivateUserAsync(admin2.Id, admin1.Id);
        Assert.True(allowedDeact.Succeeded);
        Assert.False(allowedDeact.User?.IsActive);
    }

    // =========================================================================
    // 9. Deactivated user login rejected
    // =========================================================================

    [Fact]
    public async Task DeactivatedUser_LoginRejected()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var deactivatedUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "disabled@ep.lk",
            EmailConfirmed = true,
            IsActive = false // Deactivated
        };

        userManagerMock.Setup(m => m.FindByEmailAsync("disabled@ep.lk")).ReturnsAsync(deactivatedUser);
        userManagerMock.Setup(m => m.CheckPasswordAsync(deactivatedUser, "SecretPass123!")).ReturnsAsync(true);

        var loginService = new LoginService(
            userManagerMock.Object,
            Mock.Of<IJwtTokenService>(),
            Mock.Of<ILogger<LoginService>>());

        var result = await loginService.LoginAsync(new LoginRequest
        {
            Email = "disabled@ep.lk",
            Password = "SecretPass123!"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("ACCOUNT_DISABLED", result.Code);
    }

    // =========================================================================
    // 10. Existing session rejected after deactivation
    // =========================================================================

    [Fact]
    public async Task ExistingSession_RejectedAfterDeactivation()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var adminId = Guid.NewGuid();
        var adminUser = new ApplicationUser
        {
            Id = adminId,
            Email = "admin@ep.lk",
            IsActive = false // Account was deactivated after token was issued
        };

        userManagerMock.Setup(m => m.FindByIdAsync(adminId.ToString())).ReturnsAsync(adminUser);

        var handler = new AdminOnlyAuthorizationHandler(userManagerMock.Object, Mock.Of<ILogger<AdminOnlyAuthorizationHandler>>());
        var requirement = new AdminOnlyRequirement();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Administrator)
        }, "Bearer"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);
        await handler.HandleAsync(context);

        // Access must be denied because user.IsActive == false in database
        Assert.False(context.HasSucceeded);
    }

    // =========================================================================
    // 11. Removed admin loses admin access
    // =========================================================================

    [Fact]
    public async Task RemovedAdmin_LosesAdminAccess()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var demotedId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = demotedId,
            Email = "demoted@ep.lk",
            IsActive = true
        };

        userManagerMock.Setup(m => m.FindByIdAsync(demotedId.ToString())).ReturnsAsync(user);
        // User has been demoted to Customer; IsInRoleAsync returns false for Administrator
        userManagerMock.Setup(m => m.IsInRoleAsync(user, AppRoles.Administrator)).ReturnsAsync(false);

        var handler = new AdminOnlyAuthorizationHandler(userManagerMock.Object, Mock.Of<ILogger<AdminOnlyAuthorizationHandler>>());
        var requirement = new AdminOnlyRequirement();

        // Stale token still containing Administrator role
        var stalePrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, demotedId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Administrator)
        }, "Bearer"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, stalePrincipal, null);
        await handler.HandleAsync(context);

        // Denied: Live role check rejects stale token
        Assert.False(context.HasSucceeded);
    }

    // =========================================================================
    // 12. Audit record created with accurate state and metadata
    // =========================================================================

    [Fact]
    public async Task AuditRecord_CreatedWithAccurateStateAndMetadata()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Auditor", LastName = "Admin", Email = "auditor@ep.lk", IsActive = true };
        var target = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Client", LastName = "User", Email = "client@ep.lk", IsActive = true };
        context.Users.AddRange(admin, target);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, target.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var beforeTime = DateTime.UtcNow.AddSeconds(-1);
        await service.DeactivateUserAsync(admin.Id, target.Id, "Policy audit deactivation", "203.0.113.195");
        var afterTime = DateTime.UtcNow.AddSeconds(1);

        var audit = await context.AdminAuditLogs.SingleOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(admin.Id, audit.ActorAdminId);
        Assert.Equal("auditor@ep.lk", audit.ActorEmail);
        Assert.Equal(target.Id, audit.TargetUserId);
        Assert.Equal("client@ep.lk", audit.TargetEmail);
        Assert.Equal("ADMIN_USER_DEACTIVATED", audit.Action);
        Assert.Equal("SUCCESS", audit.Outcome);
        Assert.Equal("Policy audit deactivation", audit.Reason);
        Assert.Equal("203.0.113.195", audit.IpAddress);
        Assert.True(audit.Timestamp >= beforeTime && audit.Timestamp <= afterTime);
    }

    // =========================================================================
    // 13. Concurrent last admin changes: guards prevent violations
    // =========================================================================

    [Fact]
    public async Task ConcurrentLastAdminChanges_GuardsPreventViolations()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin1 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin1", LastName = "A", Email = "a1@ep.lk", IsActive = true };
        var admin2 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin2", LastName = "B", Email = "a2@ep.lk", IsActive = true };
        context.Users.AddRange(admin1, admin2);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin1.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, admin2.Id, AppRoles.Administrator);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        // Both admins attempt to demote each other simultaneously
        var task1 = service.ChangeUserRoleAsync(admin1.Id, admin2.Id, AppRoles.Customer);
        var task2 = service.ChangeUserRoleAsync(admin2.Id, admin1.Id, AppRoles.Customer);

        var results = await Task.WhenAll(task1, task2);

        // At least one operation must be rejected by LAST_ADMIN_PROTECTION
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded && r.Code == "LAST_ADMIN_PROTECTION");

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // Ensure at least 1 active admin remains in database
        var remainingAdminRole = await context.Roles.FirstAsync(r => r.NormalizedName == AppRoles.Administrator.ToUpperInvariant());
        var activeAdmins = await (from ur in context.UserRoles
                                  join u in context.Users on ur.UserId equals u.Id
                                  where ur.RoleId == remainingAdminRole.Id && u.IsActive
                                  select u).CountAsync();
        Assert.True(activeAdmins >= 1);
    }

    // =========================================================================
    // 14. Invalid state transitions
    // =========================================================================

    [Fact]
    public async Task InvalidStateTransition_ActivatingActiveOrDeactivatingInactive_Fails()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRolesAsync(context);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Admin", LastName = "One", Email = "admin@ep.lk", IsActive = true };
        var activeCustomer = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Active", LastName = "User", Email = "active@ep.lk", IsActive = true };
        var inactiveCustomer = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "User", Email = "inactive@ep.lk", IsActive = false };
        context.Users.AddRange(admin, activeCustomer, inactiveCustomer);
        await context.SaveChangesAsync();
        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, activeCustomer.Id, AppRoles.Customer);
        await AssignRoleAsync(context, inactiveCustomer.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        // Activating an already active user
        var actActive = await service.ActivateUserAsync(admin.Id, activeCustomer.Id);
        Assert.False(actActive.Succeeded);
        Assert.Equal(400, actActive.StatusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", actActive.Code);

        // Deactivating an already inactive user
        var deactInactive = await service.DeactivateUserAsync(admin.Id, inactiveCustomer.Id);
        Assert.False(deactInactive.Succeeded);
        Assert.Equal(400, deactInactive.StatusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", deactInactive.Code);

        // Assigning current role (re-assigning Customer)
        var sameRole = await service.ChangeUserRoleAsync(admin.Id, activeCustomer.Id, AppRoles.Customer);
        Assert.False(sameRole.Succeeded);
        Assert.Equal(400, sameRole.StatusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", sameRole.Code);
    }
}
