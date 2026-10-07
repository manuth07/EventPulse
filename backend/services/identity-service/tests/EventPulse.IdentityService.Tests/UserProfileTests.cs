using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class UserProfileTests
{
    private readonly Mock<IUserProfileService> _mockProfileService;
    private readonly UsersController _controller;

    public UserProfileTests()
    {
        _mockProfileService = new Mock<IUserProfileService>();
        _controller = new UsersController(_mockProfileService.Object);
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "customer@eventpulse.com"),
            new(ClaimTypes.Role, AppRoles.Customer)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetCurrentUserProfile_WhenAuthenticated_ReturnsOkWithProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        var profileResponse = new UserProfileResponse
        {
            Id = userId,
            FirstName = "Alice",
            LastName = "Customer",
            Email = "alice@eventpulse.com",
            PhoneNumber = "+94771234567",
            CountryCode = "LK",
            ProfileCompleted = true,
            HasPassword = true,
            Role = AppRoles.Customer,
            Roles = new List<string> { AppRoles.Customer },
            CreatedAt = DateTime.UtcNow
        };

        _mockProfileService
            .Setup(s => s.GetProfileAsync(userId))
            .ReturnsAsync(ProfileResult.Success(profileResponse));

        // Act
        var result = await _controller.GetCurrentUserProfile();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedProfile = Assert.IsType<UserProfileResponse>(okResult.Value);
        Assert.Equal(userId, returnedProfile.Id);
        Assert.Equal("Alice", returnedProfile.FirstName);
        Assert.Equal(AppRoles.Customer, returnedProfile.Role);
        Assert.True(returnedProfile.HasPassword);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange — empty context with no claims
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        // Act
        var result = await _controller.GetCurrentUserProfile();

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        _mockProfileService
            .Setup(s => s.GetProfileAsync(userId))
            .ReturnsAsync(ProfileResult.NotFound());

        // Act
        var result = await _controller.GetCurrentUserProfile();

        // Assert
        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Customer)]
    [InlineData(AppRoles.Organizer)]
    [InlineData(AppRoles.Administrator)]
    public async Task GetProfileAsync_ReturnsCorrectAuthoritativeRole(string expectedRole)
    {
        // Arrange
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userRoleStoreMock = userStoreMock.As<IUserRoleStore<ApplicationUser>>();
        var userPasswordStoreMock = userStoreMock.As<IUserPasswordStore<ApplicationUser>>();

        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var userId = Guid.NewGuid();
        var testUser = new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@eventpulse.com",
            PhoneNumber = "+94770000000",
            CountryCode = "LK",
            ProfileCompleted = true,
            CreatedAt = DateTime.UtcNow
        };

        userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(testUser);

        userManagerMock
            .Setup(m => m.GetRolesAsync(testUser))
            .ReturnsAsync(new List<string> { expectedRole });

        userManagerMock
            .Setup(m => m.HasPasswordAsync(testUser))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<UserProfileService>>();
        var service = new UserProfileService(userManagerMock.Object, loggerMock.Object);

        // Act
        var result = await service.GetProfileAsync(userId);

        // Assert
        Assert.True(result.Succeeded);
        var profile = Assert.IsType<UserProfileResponse>(result.Response);
        Assert.Equal(expectedRole, profile.Role);
        Assert.Contains(expectedRole, profile.Roles);
    }

    [Fact]
    public async Task UpdateEmail_WhenValidAndUnique_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        var request = new UpdateEmailRequest { NewEmail = "newemail@eventpulse.com" };

        _mockProfileService
            .Setup(s => s.UpdateEmailAsync(userId, request.NewEmail))
            .ReturnsAsync(ProfileResult.Success(new { email = request.NewEmail, message = "Updated" }));

        // Act
        var result = await _controller.UpdateEmail(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task UpdateEmail_WhenDuplicate_ReturnsConflict()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        var request = new UpdateEmailRequest { NewEmail = "duplicate@eventpulse.com" };

        _mockProfileService
            .Setup(s => s.UpdateEmailAsync(userId, request.NewEmail))
            .ReturnsAsync(ProfileResult.Conflict("DUPLICATE_EMAIL", "Email exists."));

        // Act
        var result = await _controller.UpdateEmail(request);

        // Assert
        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task UpdatePhone_WhenValid_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        var request = new UpdatePhoneRequest { NewPhoneNumber = "+94771234567" };

        _mockProfileService
            .Setup(s => s.UpdatePhoneAsync(userId, request.NewPhoneNumber))
            .ReturnsAsync(ProfileResult.Success(new { phoneNumber = "+94771234567", message = "Updated" }));

        // Act
        var result = await _controller.UpdatePhone(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }
}
