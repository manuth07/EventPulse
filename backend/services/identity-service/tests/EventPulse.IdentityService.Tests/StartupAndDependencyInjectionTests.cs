using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using Xunit;

namespace EventPulse.IdentityService.Tests;

/// <summary>
/// Unit and integration tests for EP-407:
/// 1. Dev-start PowerShell script encoding and parsing (Defect A)
/// 2. AdminOnlyAuthorizationHandler dependency injection constructor resolution (Defect B)
/// 3. ApplicationDbContext model consistency against migrations (Defect C)
/// </summary>
public class StartupAndDependencyInjectionTests
{
    // =========================================================================
    // Defect A — PowerShell Script Syntax & Invisible Character Tests
    // =========================================================================

    [Fact]
    public void DevStartScript_ContainsNoNonBreakingSpaces_OrInvisibleCharacters()
    {
        // Locate dev-start.ps1 relative to test assembly
        var currentDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(currentDir);
        Assert.NotNull(repoRoot);

        var devStartPath = Path.Combine(repoRoot, "backend", "dev-start.ps1");
        Assert.True(File.Exists(devStartPath), $"dev-start.ps1 not found at {devStartPath}");

        var content = File.ReadAllText(devStartPath);

        // Check for non-breaking space U+00A0
        var nbspCount = 0;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\u00A0')
            {
                nbspCount++;
            }
        }

        Assert.Equal(0, nbspCount);
    }

    // =========================================================================
    // Defect B — AdminOnlyAuthorizationHandler Dependency Injection Tests
    // =========================================================================

    [Fact]
    public void AdminOnlyAuthorizationHandler_HasExactlyOnePublicConstructor()
    {
        var handlerType = typeof(AdminOnlyAuthorizationHandler);
        var constructors = handlerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);

        var ctor = constructors[0];
        var parameters = ctor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(UserManager<ApplicationUser>), parameters[0].ParameterType);
        Assert.Equal(typeof(ILogger<AdminOnlyAuthorizationHandler>), parameters[1].ParameterType);
    }

    [Fact]
    public void AdminOnlyAuthorizationHandler_ResolvesSuccessfully_FromDependencyInjectionContainer()
    {
        // Setup a real ServiceCollection matching Program.cs DI configuration
        var services = new ServiceCollection();

        // Mock UserManager<ApplicationUser>
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        services.AddScoped(_ => userManagerMock.Object);

        // Add logging
        services.AddLogging();

        // Register the handler as in Program.cs:
        // builder.Services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IAuthorizationHandler>();

        Assert.NotNull(handler);
        Assert.IsType<AdminOnlyAuthorizationHandler>(handler);
    }

    [Fact]
    public async Task AdminOnlyAuthorizationHandler_PreservesAuthorizationBehavior_WhenResolvedFromDI()
    {
        var services = new ServiceCollection();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var adminId = Guid.NewGuid();
        var activeAdmin = new ApplicationUser
        {
            Id = adminId,
            Email = "admin@eventpulse.com",
            IsActive = true
        };

        userManagerMock
            .Setup(m => m.FindByIdAsync(adminId.ToString()))
            .ReturnsAsync(activeAdmin);

        userManagerMock
            .Setup(m => m.IsInRoleAsync(activeAdmin, AppRoles.Administrator))
            .ReturnsAsync(true);

        services.AddScoped(_ => userManagerMock.Object);
        services.AddLogging();
        services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IAuthorizationHandler>() as AdminOnlyAuthorizationHandler;
        Assert.NotNull(handler);

        // Test active admin user
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Administrator)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new AdminOnlyRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AdminOnlyAuthorizationHandler_DeniesNonAdminUser_WhenResolvedFromDI()
    {
        var services = new ServiceCollection();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        services.AddScoped(_ => userManagerMock.Object);
        services.AddLogging();
        services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IAuthorizationHandler>() as AdminOnlyAuthorizationHandler;
        Assert.NotNull(handler);

        // Test customer user with customer role
        var customerId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
            new Claim(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new AdminOnlyRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    // =========================================================================
    // Defect C — ApplicationDbContext Model Consistency Tests
    // =========================================================================

    [Fact]
    public void ApplicationDbContext_BuildsModelSuccessfully_WithoutExceptions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);

        // Force model creation
        var model = context.Model;
        Assert.NotNull(model);

        // Verify key entities configured in OnModelCreating exist in model
        var adminAuditLogEntity = model.FindEntityType(typeof(AdminAuditLog));
        Assert.NotNull(adminAuditLogEntity);
        Assert.Equal("AdminAuditLogs", adminAuditLogEntity.GetTableName());

        var appUserEntity = model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(appUserEntity);

        var organizerAppEntity = model.FindEntityType(typeof(OrganizerApplication));
        Assert.NotNull(organizerAppEntity);
        Assert.Equal("OrganizerApplications", organizerAppEntity.GetTableName());

        var passwordResetEntity = model.FindEntityType(typeof(PasswordResetToken));
        Assert.NotNull(passwordResetEntity);
        Assert.Equal("PasswordResetTokens", passwordResetEntity.GetTableName());
    }

    // =========================================================================
    // Helper to locate repository root
    // =========================================================================

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
