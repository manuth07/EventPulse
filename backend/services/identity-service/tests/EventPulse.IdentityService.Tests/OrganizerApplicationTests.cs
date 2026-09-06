using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class OrganizerApplicationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static CreateOrganizerApplicationRequest ValidRequest() => new()
    {
        OrganizerName = "Pulse Productions",
        OrganizerType = "Organization",
        ContactNumber = "+94 77 123 4567",
        Description = "Premier event management and production company in Colombo.",
        Website = "https://pulseproductions.lk"
    };

    // -----------------------------------------------------------------------
    // Test Matrix A: Customer submits valid application
    // Expected: 201 Created, Status Pending, UserId matches JWT, server controlled fields
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_ValidCustomer_Returns201_StatusPending()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var loggerMock = new Mock<ILogger<OrganizerApplicationService>>();

        var userId = Guid.NewGuid();
        var customer = new ApplicationUser
        {
            Id = userId,
            Email = "customer@example.com",
            FirstName = "Kasun",
            LastName = "Silva",
            IsActive = true
        };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, loggerMock.Object);
        var request = ValidRequest();

        var (result, error, statusCode, errorCode) = await service.SubmitApplicationAsync(userId, request);

        Assert.Null(error);
        Assert.Null(errorCode);
        Assert.Equal(201, statusCode);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Pulse Productions", result.OrganizerName);
        Assert.Equal("Organization", result.OrganizerType);
        Assert.Equal("+94 77 123 4567", result.ContactNumber);
        Assert.Equal("https://pulseproductions.lk", result.Website);
        Assert.Null(result.ReviewedAt);
        Assert.Null(result.ReviewComment);

        // Verify DB persistence
        var dbApp = await context.OrganizerApplications.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.NotNull(dbApp);
        Assert.Equal(OrganizerApplicationStatus.Pending, dbApp.Status);
        Assert.Equal(userId, dbApp.UserId);
    }

    // -----------------------------------------------------------------------
    // Test Matrix B: Anonymous / No JWT
    // Expected: 401 Unauthorized
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Controller_WithoutClaims_Returns401Unauthorized()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var controller = new OrganizerApplicationsController(service, Mock.Of<ILogger<OrganizerApplicationsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext() // No User claims
            }
        };

        var submitResult = await controller.SubmitApplication(ValidRequest());
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(submitResult);
        Assert.NotNull(unauthorizedResult.Value);

        var getResult = await controller.GetMyApplication();
        var unauthorizedGet = Assert.IsType<UnauthorizedObjectResult>(getResult);
        Assert.NotNull(unauthorizedGet.Value);
    }

    // -----------------------------------------------------------------------
    // Test Matrix C: Existing Organizer applies
    // Expected: 409 Conflict, controlled rejection
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_WhenUserAlreadyOrganizer_Returns409Conflict()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var loggerMock = new Mock<ILogger<OrganizerApplicationService>>();

        var userId = Guid.NewGuid();
        var organizerUser = new ApplicationUser
        {
            Id = userId,
            Email = "organizer@example.com",
            IsActive = true
        };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(organizerUser);
        userManagerMock.Setup(m => m.GetRolesAsync(organizerUser)).ReturnsAsync(new List<string> { AppRoles.Customer, AppRoles.Organizer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, loggerMock.Object);

        var (result, error, statusCode, errorCode) = await service.SubmitApplicationAsync(userId, ValidRequest());

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("ALREADY_ORGANIZER", errorCode);
        Assert.Equal("User is already an approved Organizer.", error);
    }

    // -----------------------------------------------------------------------
    // Test Matrix D: Customer submits second application while Pending
    // Expected: 409 Conflict, only one DB record
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_WhenPendingExists_Returns409Conflict_AndDoesNotDuplicate()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var loggerMock = new Mock<ILogger<OrganizerApplicationService>>();

        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, Email = "customer@example.com", IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        // Seed existing Pending application
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = "Existing App",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "0771112233",
            Description = "Existing description",
            Status = OrganizerApplicationStatus.Pending,
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, loggerMock.Object);

        var (result, error, statusCode, errorCode) = await service.SubmitApplicationAsync(userId, ValidRequest());

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("APPLICATION_PENDING", errorCode);
        Assert.Equal("An organizer application is already pending review.", error);

        // Verify still only 1 application exists
        var count = await context.OrganizerApplications.CountAsync(a => a.UserId == userId);
        Assert.Equal(1, count);
    }

    // -----------------------------------------------------------------------
    // Test Matrix E: GET own application
    // Expected: 200 OK with correct data
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetMyApplication_ReturnsOwnApplication_WhenExists()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();

        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = "My Music Org",
            OrganizerType = OrganizerType.Organization,
            ContactNumber = "0112345678",
            Description = "Music concert events",
            Website = "https://mymusic.lk",
            Status = OrganizerApplicationStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var controller = new OrganizerApplicationsController(service, Mock.Of<ILogger<OrganizerApplicationsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, AppRoles.Customer)
                    }, "TestAuth"))
                }
            }
        };

        var actionResult = await controller.GetMyApplication();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<OrganizerApplicationDto>(okResult.Value);

        Assert.Equal("My Music Org", dto.OrganizerName);
        Assert.Equal("Pending", dto.Status);
        Assert.Equal("Organization", dto.OrganizerType);
    }

    // -----------------------------------------------------------------------
    // Test Matrix F: Different Customer cannot retrieve another user's application
    // Expected: Scoped strictly to JWT identity, returns 404 for different user
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetMyApplication_ScopedToAuthenticatedUser_Returns404ForUserWithoutApplication()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // User A has an application
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userA,
            OrganizerName = "User A Productions",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "0771234567",
            Description = "User A Description",
            Status = OrganizerApplicationStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());

        // User B requests their application
        var controller = new OrganizerApplicationsController(service, Mock.Of<ILogger<OrganizerApplicationsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userB.ToString()),
                        new Claim(ClaimTypes.Role, AppRoles.Customer)
                    }, "TestAuth"))
                }
            }
        };

        var actionResult = await controller.GetMyApplication();
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
        Assert.NotNull(notFoundResult.Value);
    }

    // -----------------------------------------------------------------------
    // Test Matrix G: Google-auth Customer
    // Expected: Same application flow works seamlessly
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_GoogleAuthCustomer_Succeeds()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var googleUser = new ApplicationUser
        {
            Id = userId,
            Email = "google.user@gmail.com",
            FirstName = "Chaminda",
            LastName = "Vaas",
            PasswordHash = null, // Google-only account
            ProfileCompleted = true,
            IsActive = true
        };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(googleUser);
        userManagerMock.Setup(m => m.GetRolesAsync(googleUser)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new CreateOrganizerApplicationRequest
        {
            OrganizerName = "Cricket Carnival Organizers",
            OrganizerType = "Individual",
            ContactNumber = "+94 71 987 6543",
            Description = "Organizing charity cricket tournaments in Sri Lanka.",
            Website = "https://charitycricket.lk"
        };

        var (result, error, statusCode, _) = await service.SubmitApplicationAsync(userId, request);

        Assert.Equal(201, statusCode);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Cricket Carnival Organizers", result.OrganizerName);
        Assert.Equal("Individual", result.OrganizerType);
    }

    // -----------------------------------------------------------------------
    // Test Matrix H: Email/password Customer
    // Expected: Same application flow works seamlessly
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_EmailPasswordCustomer_Succeeds()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var standardUser = new ApplicationUser
        {
            Id = userId,
            Email = "standard.user@example.com",
            FirstName = "Anura",
            LastName = "Kumara",
            PasswordHash = "AQAAAAEAACcQAAAAE...",
            ProfileCompleted = true,
            IsActive = true
        };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(standardUser);
        userManagerMock.Setup(m => m.GetRolesAsync(standardUser)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());

        var (result, error, statusCode, _) = await service.SubmitApplicationAsync(userId, ValidRequest());

        Assert.Equal(201, statusCode);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
    }

    // -----------------------------------------------------------------------
    // Core Security Requirement: DO NOT GRANT ORGANIZER ROLE ON SUBMISSION
    // Expected: User roles remain untouched (Customer only), never Organizer
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SubmitApplication_DoesNotGrantOrganizerRole()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, Email = "test@example.com", IsActive = true };

        var userRoles = new List<string> { AppRoles.Customer };
        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(userRoles);

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var (result, _, statusCode, _) = await service.SubmitApplicationAsync(userId, ValidRequest());

        Assert.Equal(201, statusCode);
        Assert.NotNull(result);

        // Confirm AddToRoleAsync was NEVER called for Organizer
        userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), AppRoles.Organizer), Times.Never);
        Assert.DoesNotContain(AppRoles.Organizer, userRoles);
    }

    // -----------------------------------------------------------------------
    // Validation tests: Invalid Website URL, missing fields
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("javascript:alert(1)")]
    public async Task SubmitApplication_InvalidWebsiteUrl_Returns400BadRequest(string invalidUrl)
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = ValidRequest();
        request.Website = invalidUrl;

        var (result, error, statusCode, errorCode) = await service.SubmitApplicationAsync(userId, request);

        Assert.Null(result);
        Assert.Equal(400, statusCode);
        Assert.Equal("VALIDATION_ERROR", errorCode);
        Assert.Contains("valid absolute HTTP or HTTPS URL", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownType")]
    [InlineData("Corporate")]
    public async Task SubmitApplication_InvalidOrganizerType_Returns400BadRequest(string invalidType)
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = ValidRequest();
        request.OrganizerType = invalidType;

        var (result, error, statusCode, errorCode) = await service.SubmitApplicationAsync(userId, request);

        Assert.Null(result);
        Assert.Equal(400, statusCode);
        Assert.Equal("VALIDATION_ERROR", errorCode);
        Assert.Contains("Individual", error);
    }

    // -----------------------------------------------------------------------
    // Phase 3 Tests: Customer Resubmission
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResubmitApplication_RejectedApplication_TransitionsToPending_Returns200()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, Email = "resubmit@example.com", IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        var initialAppId = Guid.NewGuid();
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = initialAppId,
            UserId = userId,
            OrganizerName = "Old Org",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "+94 77 111 2222",
            Description = "Old description.",
            Status = OrganizerApplicationStatus.Rejected,
            ReviewComment = "Please provide business details.",
            SubmittedAt = DateTime.UtcNow.AddDays(-2)
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new CreateOrganizerApplicationRequest
        {
            OrganizerName = "Updated Org Ltd",
            OrganizerType = "Organization",
            ContactNumber = "+94 77 999 8888",
            Description = "Updated comprehensive description of the organization.",
            Website = "https://updated-org.com"
        };

        var (result, error, statusCode, errorCode) = await service.ResubmitApplicationAsync(userId, request);

        Assert.NotNull(result);
        Assert.Equal(200, statusCode);
        Assert.Null(error);
        Assert.Null(errorCode);
        Assert.Equal(initialAppId, result.Id); // Same ID, not a new row
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Updated Org Ltd", result.OrganizerName);
        Assert.Equal("Organization", result.OrganizerType);

        // Verify DB persistence
        var dbApp = await context.OrganizerApplications.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.NotNull(dbApp);
        Assert.Equal(OrganizerApplicationStatus.Pending, dbApp.Status);
        Assert.Equal("Updated Org Ltd", dbApp.OrganizerName);
        Assert.Equal(1, await context.OrganizerApplications.CountAsync(a => a.UserId == userId));
    }

    [Fact]
    public async Task ResubmitApplication_PendingApplication_Returns409Conflict()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = "Pending Org",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "+94 77 111 2222",
            Description = "Pending description",
            Status = OrganizerApplicationStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var (result, error, statusCode, errorCode) = await service.ResubmitApplicationAsync(userId, ValidRequest());

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", errorCode);
    }

    [Fact]
    public async Task ResubmitApplication_ApprovedApplication_Returns409Conflict()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var userId = Guid.NewGuid();
        var customer = new ApplicationUser { Id = userId, IsActive = true };

        userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync(customer);
        userManagerMock.Setup(m => m.GetRolesAsync(customer)).ReturnsAsync(new List<string> { AppRoles.Customer });

        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = "Approved Org",
            OrganizerType = OrganizerType.Organization,
            ContactNumber = "+94 77 111 2222",
            Description = "Approved description",
            Status = OrganizerApplicationStatus.Approved
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var (result, error, statusCode, errorCode) = await service.ResubmitApplicationAsync(userId, ValidRequest());

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", errorCode);
    }

    // -----------------------------------------------------------------------
    // Phase 3 Tests: Admin Queue & Review
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAdminApplications_ReturnsEnrichedDtosWithAccountEmail()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();

        var user1 = new ApplicationUser { Id = Guid.NewGuid(), Email = "applicant1@example.com", FirstName = "A", LastName = "One" };
        var user2 = new ApplicationUser { Id = Guid.NewGuid(), Email = "applicant2@example.com", FirstName = "B", LastName = "Two" };
        context.Users.AddRange(user1, user2);

        context.OrganizerApplications.AddRange(
            new OrganizerApplication
            {
                Id = Guid.NewGuid(),
                UserId = user1.Id,
                OrganizerName = "App 1",
                OrganizerType = OrganizerType.Individual,
                ContactNumber = "0771234567",
                Description = "Desc 1",
                Status = OrganizerApplicationStatus.Pending,
                SubmittedAt = DateTime.UtcNow.AddHours(-1)
            },
            new OrganizerApplication
            {
                Id = Guid.NewGuid(),
                UserId = user2.Id,
                OrganizerName = "App 2",
                OrganizerType = OrganizerType.Organization,
                ContactNumber = "0777654321",
                Description = "Desc 2",
                Status = OrganizerApplicationStatus.Approved,
                SubmittedAt = DateTime.UtcNow.AddHours(-2)
            }
        );
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var pendingOnly = await service.GetAdminApplicationsAsync(OrganizerApplicationStatus.Pending);

        Assert.Single(pendingOnly);
        Assert.Equal("App 1", pendingOnly[0].OrganizerName);
        Assert.Equal("applicant1@example.com", pendingOnly[0].AccountEmail);

        var allApps = await service.GetAdminApplicationsAsync(null);
        Assert.Equal(2, allApps.Count);
    }

    [Fact]
    public async Task ApproveApplication_Pending_AtomicallyApprovesAndAssignsRole()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var adminId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var applicant = new ApplicationUser { Id = applicantId, Email = "applicant@example.com" };
        context.Users.Add(applicant);

        var appId = Guid.NewGuid();
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = appId,
            UserId = applicantId,
            OrganizerName = "Pending Org",
            OrganizerType = OrganizerType.Organization,
            ContactNumber = "0771234567",
            Description = "Desc",
            Status = OrganizerApplicationStatus.Pending
        });
        await context.SaveChangesAsync();

        userManagerMock.Setup(m => m.FindByIdAsync(applicantId.ToString())).ReturnsAsync(applicant);
        userManagerMock.Setup(m => m.GetRolesAsync(applicant)).ReturnsAsync(new List<string> { AppRoles.Customer });
        userManagerMock.Setup(m => m.AddToRoleAsync(applicant, AppRoles.Organizer)).ReturnsAsync(IdentityResult.Success);

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new ApproveOrganizerApplicationRequest { ReviewComment = "Approved - welcome aboard!" };

        var (result, error, statusCode, errorCode) = await service.ApproveApplicationAsync(appId, adminId, request);

        Assert.NotNull(result);
        Assert.Equal(200, statusCode);
        Assert.Null(error);
        Assert.Equal("Approved", result.Status);
        Assert.Equal("Approved - welcome aboard!", result.ReviewComment);

        // Verify DB update
        var dbApp = await context.OrganizerApplications.FindAsync(appId);
        Assert.NotNull(dbApp);
        Assert.Equal(OrganizerApplicationStatus.Approved, dbApp.Status);
        Assert.Equal(adminId, dbApp.ReviewedBy);
        Assert.NotNull(dbApp.ReviewedAt);

        // Verify AddToRoleAsync was called
        userManagerMock.Verify(m => m.AddToRoleAsync(applicant, AppRoles.Organizer), Times.Once);
    }

    [Fact]
    public async Task ApproveApplication_AlreadyApproved_Returns409Conflict()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var appId = Guid.NewGuid();

        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = appId,
            UserId = Guid.NewGuid(),
            OrganizerName = "Approved Org",
            OrganizerType = OrganizerType.Organization,
            ContactNumber = "0771234567",
            Description = "Desc",
            Status = OrganizerApplicationStatus.Approved
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var (result, error, statusCode, errorCode) = await service.ApproveApplicationAsync(appId, Guid.NewGuid(), null);

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", errorCode);
    }

    [Fact]
    public async Task RejectApplication_Pending_RequiresComment_SetsStatusRejected()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var adminId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var applicant = new ApplicationUser { Id = applicantId, Email = "applicant@example.com" };
        context.Users.Add(applicant);

        var appId = Guid.NewGuid();
        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = appId,
            UserId = applicantId,
            OrganizerName = "Incomplete Org",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "0771234567",
            Description = "Desc",
            Status = OrganizerApplicationStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new RejectOrganizerApplicationRequest
        {
            ReviewComment = "Please provide more details on past event experience."
        };

        var (result, error, statusCode, errorCode) = await service.RejectApplicationAsync(appId, adminId, request);

        Assert.NotNull(result);
        Assert.Equal(200, statusCode);
        Assert.Null(error);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("Please provide more details on past event experience.", result.ReviewComment);

        var dbApp = await context.OrganizerApplications.FindAsync(appId);
        Assert.NotNull(dbApp);
        Assert.Equal(OrganizerApplicationStatus.Rejected, dbApp.Status);
        Assert.Equal(adminId, dbApp.ReviewedBy);
        Assert.Equal("Please provide more details on past event experience.", dbApp.ReviewComment);
    }

    [Fact]
    public async Task RejectApplication_BlankComment_Returns400BadRequest()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new RejectOrganizerApplicationRequest { ReviewComment = "   " };

        var (result, error, statusCode, errorCode) = await service.RejectApplicationAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Null(result);
        Assert.Equal(400, statusCode);
        Assert.Equal("VALIDATION_ERROR", errorCode);
        Assert.Contains("Rejection feedback comment is required", error);
    }

    [Fact]
    public async Task RejectApplication_AlreadyRejected_Returns409Conflict()
    {
        using var context = CreateContext();
        var userManagerMock = CreateUserManagerMock();
        var appId = Guid.NewGuid();

        context.OrganizerApplications.Add(new OrganizerApplication
        {
            Id = appId,
            UserId = Guid.NewGuid(),
            OrganizerName = "Rejected Org",
            OrganizerType = OrganizerType.Individual,
            ContactNumber = "0771234567",
            Description = "Desc",
            Status = OrganizerApplicationStatus.Rejected
        });
        await context.SaveChangesAsync();

        var service = new OrganizerApplicationService(context, userManagerMock.Object, Mock.Of<ILogger<OrganizerApplicationService>>());
        var request = new RejectOrganizerApplicationRequest { ReviewComment = "Some comment" };

        var (result, error, statusCode, errorCode) = await service.RejectApplicationAsync(appId, Guid.NewGuid(), request);

        Assert.Null(result);
        Assert.Equal(409, statusCode);
        Assert.Equal("INVALID_STATE_TRANSITION", errorCode);
    }
}
