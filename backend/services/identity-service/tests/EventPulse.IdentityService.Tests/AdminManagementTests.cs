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
/// Unit and integration tests for EP-370 — Admin user list, details, and platform overview APIs.
/// </summary>
public class AdminManagementTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedRoleAsync(ApplicationDbContext context, string roleName)
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        };
        context.Roles.Add(role);
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
        ClaimsPrincipal? principal = null)
    {
        var logger = new Mock<ILogger<AdminUsersController>>();
        var controller = new AdminUsersController(service, logger.Object);

        var httpContext = new DefaultHttpContext();
        if (principal != null)
        {
            httpContext.User = principal;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // =========================================================================
    // 1. Admin retrieves users
    // =========================================================================

    [Fact]
    public async Task AdminRetrievesUsers_ReturnsPaginatedList()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRoleAsync(context, AppRoles.Customer);
        await SeedRoleAsync(context, AppRoles.Organizer);
        await SeedRoleAsync(context, AppRoles.Administrator);

        var user1 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Alice",
            LastName = "Silva",
            Email = "alice@example.com",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Bob",
            LastName = "Perera",
            Email = "bob@example.com",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, user1.Id, AppRoles.Customer);
        await AssignRoleAsync(context, user2.Id, AppRoles.Organizer);

        var metricsMock = new Mock<IPlatformMetricsClient>();
        var loggerMock = new Mock<ILogger<AdminManagementService>>();
        var service = new AdminManagementService(context, metricsMock.Object, loggerMock.Object);

        var result = await service.GetUsersAsync(new AdminUserQueryParameters { Page = 1, PageSize = 10 });

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, u => u.Email == "alice@example.com" && u.Role == AppRoles.Customer);
        Assert.Contains(result.Items, u => u.Email == "bob@example.com" && u.Role == AppRoles.Organizer);
    }

    // =========================================================================
    // 2. Search by name
    // =========================================================================

    [Fact]
    public async Task SearchByName_FiltersCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRoleAsync(context, AppRoles.Customer);

        var user1 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Kasun", LastName = "Kalhara", Email = "kasun@test.lk", IsActive = true };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Nimal", LastName = "Siripala", Email = "nimal@test.lk", IsActive = true };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        var metricsMock = new Mock<IPlatformMetricsClient>();
        var service = new AdminManagementService(context, metricsMock.Object, Mock.Of<ILogger<AdminManagementService>>());

        // Search by first name
        var resultFirst = await service.GetUsersAsync(new AdminUserQueryParameters { Q = "kasun" });
        Assert.Single(resultFirst.Items);
        Assert.Equal("Kasun", resultFirst.Items[0].FirstName);

        // Search by last name
        var resultLast = await service.GetUsersAsync(new AdminUserQueryParameters { Q = "siripala" });
        Assert.Single(resultLast.Items);
        Assert.Equal("Nimal", resultLast.Items[0].FirstName);

        // Search full name substring
        var resultFull = await service.GetUsersAsync(new AdminUserQueryParameters { Q = "Kasun Kalhara" });
        Assert.Single(resultFull.Items);
    }

    // =========================================================================
    // 3. Search by email
    // =========================================================================

    [Fact]
    public async Task SearchByEmail_FiltersCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var user1 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "User", LastName = "One", Email = "organizer.prime@eventpulse.lk", IsActive = true };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "User", LastName = "Two", Email = "customer.alpha@eventpulse.lk", IsActive = true };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.GetUsersAsync(new AdminUserQueryParameters { Q = "organizer.prime" });
        Assert.Single(result.Items);
        Assert.Equal("organizer.prime@eventpulse.lk", result.Items[0].Email);
    }

    // =========================================================================
    // 4. Role filter
    // =========================================================================

    [Fact]
    public async Task RoleFilter_ReturnsUsersMatchingRole()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRoleAsync(context, AppRoles.Customer);
        await SeedRoleAsync(context, AppRoles.Organizer);
        await SeedRoleAsync(context, AppRoles.Administrator);

        var admin = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Super", LastName = "Admin", Email = "admin@ep.lk", IsActive = true };
        var organizer = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Event", LastName = "Host", Email = "host@ep.lk", IsActive = true };
        var customer = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Ticket", LastName = "Buyer", Email = "buyer@ep.lk", IsActive = true };

        context.Users.AddRange(admin, organizer, customer);
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, admin.Id, AppRoles.Administrator);
        await AssignRoleAsync(context, organizer.Id, AppRoles.Organizer);
        await AssignRoleAsync(context, customer.Id, AppRoles.Customer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var adminResult = await service.GetUsersAsync(new AdminUserQueryParameters { Role = "Administrator" });
        Assert.Single(adminResult.Items);
        Assert.Equal("admin@ep.lk", adminResult.Items[0].Email);

        var orgResult = await service.GetUsersAsync(new AdminUserQueryParameters { Role = "Organizer" });
        Assert.Single(orgResult.Items);
        Assert.Equal("host@ep.lk", orgResult.Items[0].Email);

        var custResult = await service.GetUsersAsync(new AdminUserQueryParameters { Role = "Customer" });
        Assert.Single(custResult.Items);
        Assert.Equal("buyer@ep.lk", custResult.Items[0].Email);
    }

    // =========================================================================
    // 5. Status filter
    // =========================================================================

    [Fact]
    public async Task StatusFilter_ActiveInactiveUnverified_FiltersCorrectly()
    {
        using var context = CreateInMemoryDbContext();

        var activeUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "A", LastName = "A", Email = "a@ep.lk", IsActive = true, EmailConfirmed = true };
        var inactiveUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "B", LastName = "B", Email = "b@ep.lk", IsActive = false, EmailConfirmed = true };
        var unverifiedUser = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "C", LastName = "C", Email = "c@ep.lk", IsActive = true, EmailConfirmed = false };

        context.Users.AddRange(activeUser, inactiveUser, unverifiedUser);
        await context.SaveChangesAsync();

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var activeResult = await service.GetUsersAsync(new AdminUserQueryParameters { Status = "Active" });
        Assert.Equal(2, activeResult.TotalCount); // activeUser and unverifiedUser both have IsActive = true

        var inactiveResult = await service.GetUsersAsync(new AdminUserQueryParameters { Status = "Inactive" });
        Assert.Single(inactiveResult.Items);
        Assert.Equal("b@ep.lk", inactiveResult.Items[0].Email);

        var unverifiedResult = await service.GetUsersAsync(new AdminUserQueryParameters { Status = "Unverified" });
        Assert.Single(unverifiedResult.Items);
        Assert.Equal("c@ep.lk", unverifiedResult.Items[0].Email);
    }

    // =========================================================================
    // 6. Pagination
    // =========================================================================

    [Fact]
    public async Task Pagination_CalculatesMetadataAndPageItemsCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        for (int i = 1; i <= 15; i++)
        {
            context.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = $"User{i:D2}",
                LastName = "Test",
                Email = $"user{i:D2}@test.lk",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await context.SaveChangesAsync();

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        // Page 1 of size 5
        var page1 = await service.GetUsersAsync(new AdminUserQueryParameters { Page = 1, PageSize = 5 });
        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(5, page1.PageSize);
        Assert.Equal(3, page1.TotalPages);
        Assert.False(page1.HasPreviousPage);
        Assert.True(page1.HasNextPage);

        // Page 2 of size 5
        var page2 = await service.GetUsersAsync(new AdminUserQueryParameters { Page = 2, PageSize = 5 });
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(2, page2.Page);
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);

        // Ensure page 1 and page 2 items are disjoint
        var page1Emails = page1.Items.Select(x => x.Email).ToHashSet();
        Assert.DoesNotContain(page2.Items[0].Email, page1Emails);

        // Page 3 of size 5
        var page3 = await service.GetUsersAsync(new AdminUserQueryParameters { Page = 3, PageSize = 5 });
        Assert.Equal(5, page3.Items.Count);
        Assert.Equal(3, page3.Page);
        Assert.True(page3.HasPreviousPage);
        Assert.False(page3.HasNextPage);
    }

    // =========================================================================
    // 7. Empty results
    // =========================================================================

    [Fact]
    public async Task EmptyResults_ReturnsEmptyPageWithZeroCount()
    {
        using var context = CreateInMemoryDbContext();
        context.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "User",
            Email = "user@test.lk",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var result = await service.GetUsersAsync(new AdminUserQueryParameters { Q = "NonExistentNameXYZ123" });

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    // =========================================================================
    // 8. User details
    // =========================================================================

    [Fact]
    public async Task UserDetails_ExistingUserWithOrganizerApp_ReturnsCompleteDetails()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRoleAsync(context, AppRoles.Organizer);

        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = "Kamal",
            LastName = "Gunaratne",
            Email = "kamal@organizer.lk",
            PhoneNumber = "+94771234567",
            CountryCode = "LK",
            IsActive = true,
            EmailConfirmed = true,
            ProfileCompleted = true,
            CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            LockoutEnd = null,
            AccessFailedCount = 0
        };

        var app = new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = "Colombo Events Ltd",
            OrganizerType = OrganizerType.Organization,
            ContactNumber = "+94771234567",
            Description = "Corporate event planner",
            Status = OrganizerApplicationStatus.Approved,
            SubmittedAt = DateTime.UtcNow.AddDays(-10),
            ReviewComment = "Verified business registration"
        };

        context.Users.Add(user);
        context.OrganizerApplications.Add(app);
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, userId, AppRoles.Organizer);

        var service = new AdminManagementService(context, Mock.Of<IPlatformMetricsClient>(), Mock.Of<ILogger<AdminManagementService>>());

        var details = await service.GetUserByIdAsync(userId);

        Assert.NotNull(details);
        Assert.Equal(userId, details.Id);
        Assert.Equal("Kamal", details.FirstName);
        Assert.Equal("Gunaratne", details.LastName);
        Assert.Equal("Kamal Gunaratne", details.FullName);
        Assert.Equal("kamal@organizer.lk", details.Email);
        Assert.Equal(AppRoles.Organizer, details.Role);
        Assert.Contains(AppRoles.Organizer, details.Roles);
        Assert.True(details.IsActive);
        Assert.True(details.EmailConfirmed);
        Assert.NotNull(details.OrganizerApplication);
        Assert.Equal("Colombo Events Ltd", details.OrganizerApplication.OrganizerName);
        Assert.Equal("Approved", details.OrganizerApplication.Status);
    }

    // =========================================================================
    // 9. Invalid user ID and 404
    // =========================================================================

    [Fact]
    public async Task InvalidUserId_ReturnsBadRequest_And_NonExistentReturnsNotFound()
    {
        var serviceMock = new Mock<IAdminManagementService>();
        var controller = BuildController(serviceMock.Object);

        // Non-Guid string
        var badResult = await controller.GetUserDetails("invalid-guid-string");
        var badRequest = Assert.IsType<BadRequestObjectResult>(badResult);
        var badJson = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_USER_ID", badJson);

        // Empty Guid
        var emptyResult = await controller.GetUserDetails(Guid.Empty.ToString());
        Assert.IsType<BadRequestObjectResult>(emptyResult);

        // Valid Guid that does not exist
        var missingId = Guid.NewGuid();
        serviceMock.Setup(s => s.GetUserByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminUserDetailsDto?)null);

        var notFoundResult = await controller.GetUserDetails(missingId.ToString());
        var notFound = Assert.IsType<NotFoundObjectResult>(notFoundResult);
        var notFoundJson = JsonSerializer.Serialize(notFound.Value);
        Assert.Contains("USER_NOT_FOUND", notFoundJson);
    }

    // =========================================================================
    // 10. Customer/Organizer denied
    // =========================================================================

    [Fact]
    public void AdminEndpoints_EnforceAdminOnlyPolicyAttribute()
    {
        var controllerType = typeof(AdminUsersController);

        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.AdminOnly, authAttr.Policy);

        // Methods inherit class authorization
        var getAction = controllerType.GetMethod(nameof(AdminUsersController.GetUsers));
        Assert.NotNull(getAction);
        Assert.False(getAction.IsDefined(typeof(AllowAnonymousAttribute), inherit: true));

        var getDetailsAction = controllerType.GetMethod(nameof(AdminUsersController.GetUserDetails));
        Assert.NotNull(getDetailsAction);
        Assert.False(getDetailsAction.IsDefined(typeof(AllowAnonymousAttribute), inherit: true));

        var getOverviewAction = controllerType.GetMethod(nameof(AdminUsersController.GetOverview));
        Assert.NotNull(getOverviewAction);
        Assert.False(getOverviewAction.IsDefined(typeof(AllowAnonymousAttribute), inherit: true));
    }

    // =========================================================================
    // 11. Anonymous request denied
    // =========================================================================

    [Fact]
    public async Task AdminOnlyAuthorizationHandler_DeniesAnonymousAndNonAdminPrincipals()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var handler = new AdminOnlyAuthorizationHandler(userManagerMock.Object, Mock.Of<ILogger<AdminOnlyAuthorizationHandler>>());
        var requirement = new AdminOnlyRequirement();

        // Anonymous / Unauthenticated
        var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var anonContext = new AuthorizationHandlerContext(new[] { requirement }, anonPrincipal, null);
        await handler.HandleAsync(anonContext);
        Assert.False(anonContext.HasSucceeded);

        // Authenticated Customer
        var customerClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Customer)
        };
        var customerPrincipal = new ClaimsPrincipal(new ClaimsIdentity(customerClaims, "Bearer"));
        var customerContext = new AuthorizationHandlerContext(new[] { requirement }, customerPrincipal, null);
        await handler.HandleAsync(customerContext);
        Assert.False(customerContext.HasSucceeded);

        // Authenticated Organizer
        var orgClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Organizer)
        };
        var orgPrincipal = new ClaimsPrincipal(new ClaimsIdentity(orgClaims, "Bearer"));
        var orgContext = new AuthorizationHandlerContext(new[] { requirement }, orgPrincipal, null);
        await handler.HandleAsync(orgContext);
        Assert.False(orgContext.HasSucceeded);
    }

    // =========================================================================
    // 12. Sensitive fields excluded
    // =========================================================================

    [Fact]
    public void AdminDTOs_NeverExposePasswordHashSecurityStampOrTokens()
    {
        var summaryDto = new AdminUserSummaryDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Role = AppRoles.Customer
        };

        var detailsDto = new AdminUserDetailsDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Role = AppRoles.Customer
        };

        var summaryJson = JsonSerializer.Serialize(summaryDto);
        var detailsJson = JsonSerializer.Serialize(detailsDto);

        // JSON serialization checks
        Assert.DoesNotContain("passwordHash", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", summaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", summaryJson, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("passwordHash", detailsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", detailsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", detailsJson, StringComparison.OrdinalIgnoreCase);

        // Reflection property inspection
        var summaryProperties = typeof(AdminUserSummaryDto).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("passwordhash", summaryProperties);
        Assert.DoesNotContain("securitystamp", summaryProperties);
        Assert.DoesNotContain("concurrencystamp", summaryProperties);

        var detailsProperties = typeof(AdminUserDetailsDto).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("passwordhash", detailsProperties);
        Assert.DoesNotContain("securitystamp", detailsProperties);
        Assert.DoesNotContain("concurrencystamp", detailsProperties);
    }

    // =========================================================================
    // 13. Platform statistics correctness
    // =========================================================================

    [Fact]
    public async Task PlatformStatistics_CalculatesIdentityAndCrossServiceMetricsCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        await SeedRoleAsync(context, AppRoles.Customer);
        await SeedRoleAsync(context, AppRoles.Organizer);
        await SeedRoleAsync(context, AppRoles.Administrator);

        var adminId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var cust1Id = Guid.NewGuid();
        var cust2Id = Guid.NewGuid();

        context.Users.AddRange(
            new ApplicationUser { Id = adminId, FirstName = "Admin", LastName = "One", Email = "admin@ep.lk", IsActive = true },
            new ApplicationUser { Id = orgId, FirstName = "Org", LastName = "One", Email = "org@ep.lk", IsActive = true },
            new ApplicationUser { Id = cust1Id, FirstName = "Cust", LastName = "One", Email = "cust1@ep.lk", IsActive = true },
            new ApplicationUser { Id = cust2Id, FirstName = "Cust", LastName = "Two", Email = "cust2@ep.lk", IsActive = false } // 1 inactive
        );
        await context.SaveChangesAsync();

        await AssignRoleAsync(context, adminId, AppRoles.Administrator);
        await AssignRoleAsync(context, orgId, AppRoles.Organizer);
        await AssignRoleAsync(context, cust1Id, AppRoles.Customer);
        await AssignRoleAsync(context, cust2Id, AppRoles.Customer);

        // Add 1 pending organizer application
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = cust1Id,
            OrganizerName = "Pending Org",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "+94770000000",
            Description = "Wants to organize",
            Status = OrganizerApplicationStatus.Pending
        });
        await context.SaveChangesAsync();

        var metricsMock = new Mock<IPlatformMetricsClient>();
        metricsMock.Setup(m => m.GetEventsMetricAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.EventService",
                TotalCount = 42,
                Status = "Available",
                Message = "Live count"
            });

        metricsMock.Setup(m => m.GetBookingsMetricAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.BookingService",
                TotalCount = null,
                Status = "Unavailable",
                Message = "Booking Service metric unavailable"
            });

        var service = new AdminManagementService(context, metricsMock.Object, Mock.Of<ILogger<AdminManagementService>>());

        var overview = await service.GetPlatformOverviewAsync();

        Assert.NotNull(overview);
        Assert.Equal("EventPulse.IdentityService", overview.Users.OwningService);
        Assert.Equal(4, overview.Users.TotalUsers);
        Assert.Equal(3, overview.Users.ActiveUsers);
        Assert.Equal(1, overview.Users.InactiveUsers);
        Assert.Equal(1, overview.Users.TotalAdministrators);
        Assert.Equal(1, overview.Users.TotalOrganizers);
        Assert.Equal(2, overview.Users.TotalCustomers);
        Assert.Equal(1, overview.Users.PendingOrganizerApplications);

        // Downstream Event metric
        Assert.Equal("EventPulse.EventService", overview.Events.OwningService);
        Assert.Equal(42, overview.Events.TotalCount);
        Assert.Equal("Available", overview.Events.Status);

        // Downstream Booking metric (transparent dependency failure reporting)
        Assert.Equal("EventPulse.BookingService", overview.Bookings.OwningService);
        Assert.Null(overview.Bookings.TotalCount);
        Assert.Equal("Unavailable", overview.Bookings.Status);
    }
}
