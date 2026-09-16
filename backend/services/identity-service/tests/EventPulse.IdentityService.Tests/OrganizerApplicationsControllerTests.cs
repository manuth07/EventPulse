using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.IdentityService.Tests;

/// <summary>
/// Controller-level tests for OrganizerApplicationsController, exercising the actual
/// action methods (role/claim wiring, route binding, response mapping) with a mocked
/// IOrganizerApplicationService — unlike the existing OrganizerApplicationTests.cs,
/// which calls the service directly and never touches the controller at all.
/// </summary>
public class OrganizerApplicationsControllerTests
{
    private static OrganizerApplicationsController BuildController(
        Mock<IOrganizerApplicationService> serviceMock,
        Guid? userId = null)
    {
        var logger = new Mock<ILogger<OrganizerApplicationsController>>();
        var controller = new OrganizerApplicationsController(serviceMock.Object, logger.Object);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) };
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // -------------------------------------------------------------------------------
    // POST /api/organizer-applications — SubmitApplication
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task SubmitApplication_NoUserClaim_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        var controller = BuildController(serviceMock, userId: null);

        var result = await controller.SubmitApplication(new CreateOrganizerApplicationRequest());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);
        serviceMock.Verify(s => s.SubmitApplicationAsync(
            It.IsAny<Guid>(), It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitApplication_ValidRequest_ReturnsCreatedAtAction()
    {
        var userId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.SubmitApplicationAsync(userId, It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new OrganizerApplicationDto(), (string?)null, 201, (string?)null));

        var controller = BuildController(serviceMock, userId);

        var result = await controller.SubmitApplication(new CreateOrganizerApplicationRequest());

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(OrganizerApplicationsController.GetMyApplication), created.ActionName);
        serviceMock.Verify(s => s.SubmitApplicationAsync(userId, It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitApplication_ServiceReturnsError_ReturnsMappedStatusCode()
    {
        var userId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.SubmitApplicationAsync(userId, It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((OrganizerApplicationDto?)null, "An application already exists for this user.", 409, "APPLICATION_ALREADY_EXISTS"));

        var controller = BuildController(serviceMock, userId);

        var result = await controller.SubmitApplication(new CreateOrganizerApplicationRequest());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // GET /api/organizer-applications/me — GetMyApplication
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task GetMyApplication_NoUserClaim_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        var controller = BuildController(serviceMock, userId: null);

        var result = await controller.GetMyApplication();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetMyApplication_NoExistingApplication_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetMyApplicationAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizerApplicationDto?)null);

        var controller = BuildController(serviceMock, userId);

        var result = await controller.GetMyApplication();

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    [Fact]
    public async Task GetMyApplication_ExistingApplication_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var dto = new OrganizerApplicationDto();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetMyApplicationAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = BuildController(serviceMock, userId);

        var result = await controller.GetMyApplication();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
    }

    // -------------------------------------------------------------------------------
    // PUT/POST /api/organizer-applications/me/resubmit — ResubmitApplication
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task ResubmitApplication_NoUserClaim_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        var controller = BuildController(serviceMock, userId: null);

        var result = await controller.ResubmitApplication(new CreateOrganizerApplicationRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ResubmitApplication_ValidRequest_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var dto = new OrganizerApplicationDto();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.ResubmitApplicationAsync(userId, It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((dto, (string?)null, 200, (string?)null));

        var controller = BuildController(serviceMock, userId);

        var result = await controller.ResubmitApplication(new CreateOrganizerApplicationRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task ResubmitApplication_NonRejectedApplication_ReturnsConflict()
    {
        var userId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.ResubmitApplicationAsync(userId, It.IsAny<CreateOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((OrganizerApplicationDto?)null, "Only Rejected applications can be resubmitted.", 409, "INVALID_STATE"));

        var controller = BuildController(serviceMock, userId);

        var result = await controller.ResubmitApplication(new CreateOrganizerApplicationRequest());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // GET admin listing/detail — role-gated via [Authorize(Policy="AdministratorOnly")],
    // which ASP.NET's authorization middleware enforces before the action runs, so these
    // tests confirm response mapping assuming the policy already passed (the policy
    // enforcement itself belongs in an integration test, not here).
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task GetAdminApplications_NoStatusFilter_ReturnsOkWithList()
    {
        var applications = new List<AdminOrganizerApplicationDto> { new(), new() };
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetAdminApplicationsAsync(It.IsAny<OrganizerApplicationStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(applications);

        var controller = BuildController(serviceMock);

        var result = await controller.GetAdminApplications(status: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(applications, ok.Value);
    }

    [Fact]
    public async Task GetAdminApplications_WithStatusFilter_PassesParsedEnumToService()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetAdminApplicationsAsync(OrganizerApplicationStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdminOrganizerApplicationDto>());

        var controller = BuildController(serviceMock);

        await controller.GetAdminApplications(status: "Approved");

        serviceMock.Verify(s => s.GetAdminApplicationsAsync(OrganizerApplicationStatus.Approved, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminApplicationById_NotFound_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetAdminApplicationByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminOrganizerApplicationDto?)null);

        var controller = BuildController(serviceMock);

        var result = await controller.GetAdminApplicationById(applicationId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    [Fact]
    public async Task GetAdminApplicationById_Found_ReturnsOk()
    {
        var applicationId = Guid.NewGuid();
        var dto = new AdminOrganizerApplicationDto();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.GetAdminApplicationByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = BuildController(serviceMock);

        var result = await controller.GetAdminApplicationById(applicationId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
    }

    // -------------------------------------------------------------------------------
    // POST admin/{id}/approve — ApproveApplication
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task ApproveApplication_NoReviewerClaim_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        var controller = BuildController(serviceMock, userId: null);

        var result = await controller.ApproveApplication(Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
        serviceMock.Verify(s => s.ApproveApplicationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ApproveOrganizerApplicationRequest?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveApplication_Success_ReturnsOkAndPassesReviewerId()
    {
        var applicationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var dto = new AdminOrganizerApplicationDto();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.ApproveApplicationAsync(applicationId, adminId, It.IsAny<ApproveOrganizerApplicationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((dto, (string?)null, 200, (string?)null));

        var controller = BuildController(serviceMock, adminId);

        var result = await controller.ApproveApplication(applicationId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
        serviceMock.Verify(s => s.ApproveApplicationAsync(applicationId, adminId, It.IsAny<ApproveOrganizerApplicationRequest?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveApplication_AlreadyReviewed_ReturnsConflict()
    {
        var applicationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.ApproveApplicationAsync(applicationId, adminId, It.IsAny<ApproveOrganizerApplicationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((AdminOrganizerApplicationDto?)null, "Only Pending applications can be approved.", 409, "INVALID_STATE"));

        var controller = BuildController(serviceMock, adminId);

        var result = await controller.ApproveApplication(applicationId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // POST admin/{id}/reject — RejectApplication
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task RejectApplication_NoReviewerClaim_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerApplicationService>();
        var controller = BuildController(serviceMock, userId: null);

        var result = await controller.RejectApplication(Guid.NewGuid(), new RejectOrganizerApplicationRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
        serviceMock.Verify(s => s.RejectApplicationAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RejectOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectApplication_Success_ReturnsOk()
    {
        var applicationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var dto = new AdminOrganizerApplicationDto();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.RejectApplicationAsync(applicationId, adminId, It.IsAny<RejectOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((dto, (string?)null, 200, (string?)null));

        var controller = BuildController(serviceMock, adminId);

        var result = await controller.RejectApplication(applicationId, new RejectOrganizerApplicationRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task RejectApplication_NonPendingApplication_ReturnsConflict()
    {
        var applicationId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var serviceMock = new Mock<IOrganizerApplicationService>();
        serviceMock.Setup(s => s.RejectApplicationAsync(applicationId, adminId, It.IsAny<RejectOrganizerApplicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((AdminOrganizerApplicationDto?)null, "Only Pending applications can be rejected.", 409, "INVALID_STATE"));

        var controller = BuildController(serviceMock, adminId);

        var result = await controller.RejectApplication(applicationId, new RejectOrganizerApplicationRequest());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }
}