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

    [Fact]
    public async Task ChangePassword_WhenValid_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        _mockProfileService
            .Setup(s => s.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ProfileResult.Success(new { message = "Password changed successfully." }));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task ChangePassword_WhenCurrentPasswordIncorrect_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        _mockProfileService
            .Setup(s => s.ChangePasswordAsync(userId, request))
            .ReturnsAsync(ProfileResult.BadRequest("INCORRECT_CURRENT_PASSWORD", "The current password provided is incorrect."));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WhenValidFile_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        var fileMock = new Mock<IFormFile>();
        var content = "fake image content";
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns("avatar.png");
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("image/png");

        _mockProfileService
            .Setup(s => s.UpdateAvatarAsync(userId, It.IsAny<Stream>(), "image/png", "avatar.png"))
            .ReturnsAsync(ProfileResult.Success(new { profilePictureUrl = "/avatars/test.png", message = "Updated" }));

        // Act
        var result = await _controller.UploadAvatar(fileMock.Object);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task RemoveAvatar_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        _mockProfileService
            .Setup(s => s.RemoveAvatarAsync(userId))
            .ReturnsAsync(ProfileResult.Success(new { profilePictureUrl = (string?)null, message = "Removed" }));

        // Act
        var result = await _controller.RemoveAvatar();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    // =========================================================================
    // EP-406: PUT /api/users/profile Tests
    // =========================================================================

    [Fact]
    public async Task UpdateProfile_WhenAuthenticated_ReturnsOkWithUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester",
            PhoneNumber = "+94771234567"
        };

        var expectedProfile = new UserProfileResponse
        {
            Id = userId,
            FirstName = "QA",
            LastName = "Tester",
            PhoneNumber = "+94771234567",
            Email = "customer@eventpulse.com",
            Role = AppRoles.Customer,
            Roles = new List<string> { AppRoles.Customer },
            ProfileCompleted = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockProfileService
            .Setup(s => s.UpdateProfileAsync(userId, request))
            .ReturnsAsync(ProfileResult.Success(expectedProfile));

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<UserProfileResponse>(okResult.Value);
        Assert.Equal("QA", profile.FirstName);
        Assert.Equal("Tester", profile.LastName);
        Assert.Equal("+94771234567", profile.PhoneNumber);
    }

    [Fact]
    public async Task UpdateProfile_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange — empty context with no claims
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester",
            PhoneNumber = "+94771234567"
        };

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);
        _mockProfileService.Verify(s => s.UpdateProfileAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_DerivesUserIdFromJwt_CannotUpdateAnotherUser()
    {
        // Arrange
        var authenticUserId = Guid.NewGuid();
        SetUserContext(authenticUserId);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "Hacker",
            LastName = "Attempt",
            PhoneNumber = "+94779999999"
        };

        _mockProfileService
            .Setup(s => s.UpdateProfileAsync(authenticUserId, request))
            .ReturnsAsync(ProfileResult.Success(new UserProfileResponse { Id = authenticUserId, FirstName = "Hacker", LastName = "Attempt" }));

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert: The service is called strictly with the JWT-derived userId (authenticUserId), never any other id
        _mockProfileService.Verify(s => s.UpdateProfileAsync(authenticUserId, request), Times.Once);
        _mockProfileService.Verify(s => s.UpdateProfileAsync(It.Is<Guid>(id => id != authenticUserId), It.IsAny<UpdateUserProfileRequest>()), Times.Never);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateProfile_WhenModelStateInvalid_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);
        _controller.ModelState.AddModelError("FirstName", "First name is required.");

        var request = new UpdateUserProfileRequest
        {
            FirstName = "",
            LastName = "Tester",
            PhoneNumber = "+94771234567"
        };

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _mockProfileService.Verify(s => s.UpdateProfileAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester",
            PhoneNumber = "+94771234567"
        };

        _mockProfileService
            .Setup(s => s.UpdateProfileAsync(userId, request))
            .ReturnsAsync(ProfileResult.NotFound());

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WhenServiceFails_ReturnsServerError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetUserContext(userId);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester",
            PhoneNumber = "+94771234567"
        };

        _mockProfileService
            .Setup(s => s.UpdateProfileAsync(userId, request))
            .ReturnsAsync(ProfileResult.ServerError());

        // Act
        var result = await _controller.UpdateProfile(request);

        // Assert
        var serverError = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, serverError.StatusCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_PersistsFirstNameLastNameAndPhone()
    {
        // Arrange
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userRoleStoreMock = userStoreMock.As<IUserRoleStore<ApplicationUser>>();
        var userPasswordStoreMock = userStoreMock.As<IUserPasswordStore<ApplicationUser>>();

        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var userId = Guid.NewGuid();
        var existingUser = new ApplicationUser
        {
            Id = userId,
            FirstName = "Original",
            LastName = "Name",
            Email = "original@eventpulse.com",
            PhoneNumber = "+94770000000",
            CountryCode = "LK",
            ProfileCompleted = true,
            CreatedAt = DateTime.UtcNow
        };

        userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(existingUser);

        userManagerMock
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        userManagerMock
            .Setup(m => m.GetRolesAsync(existingUser))
            .ReturnsAsync(new List<string> { AppRoles.Customer });

        userManagerMock
            .Setup(m => m.HasPasswordAsync(existingUser))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<UserProfileService>>();
        var service = new UserProfileService(userManagerMock.Object, loggerMock.Object);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "  QA  ",
            LastName = "  Tester  ",
            PhoneNumber = " +94 77 123 4567 "
        };

        // Act
        var result = await service.UpdateProfileAsync(userId, request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("QA", existingUser.FirstName);
        Assert.Equal("Tester", existingUser.LastName);
        Assert.Equal("+94771234567", existingUser.PhoneNumber);

        var profile = Assert.IsType<UserProfileResponse>(result.Response);
        Assert.Equal("QA", profile.FirstName);
        Assert.Equal("Tester", profile.LastName);
        Assert.Equal("+94771234567", profile.PhoneNumber);
        Assert.Equal(AppRoles.Customer, profile.Role);

        userManagerMock.Verify(m => m.UpdateAsync(existingUser), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenNamesEmpty_ReturnsBadRequest()
    {
        // Arrange
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var loggerMock = new Mock<ILogger<UserProfileService>>();
        var service = new UserProfileService(userManagerMock.Object, loggerMock.Object);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "   ",
            LastName = "   "
        };

        // Act
        var result = await service.UpdateProfileAsync(Guid.NewGuid(), request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_NAME", result.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var loggerMock = new Mock<ILogger<UserProfileService>>();
        var service = new UserProfileService(userManagerMock.Object, loggerMock.Object);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester"
        };

        // Act
        var result = await service.UpdateProfileAsync(Guid.NewGuid(), request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenDatabaseUpdateFails_ReturnsServerError()
    {
        // Arrange
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var userId = Guid.NewGuid();
        var existingUser = new ApplicationUser
        {
            Id = userId,
            FirstName = "Existing",
            LastName = "User"
        };

        userManagerMock
            .Setup(m => m.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(existingUser);

        userManagerMock
            .Setup(m => m.UpdateAsync(existingUser))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Database lock error" }));

        var loggerMock = new Mock<ILogger<UserProfileService>>();
        var service = new UserProfileService(userManagerMock.Object, loggerMock.Object);

        var request = new UpdateUserProfileRequest
        {
            FirstName = "QA",
            LastName = "Tester"
        };

        // Act
        var result = await service.UpdateProfileAsync(userId, request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void GatewayConfiguration_VerifiesUserProfileRouting()
    {
        // Arrange: Find gateway appsettings.json
        var baseDir = AppContext.BaseDirectory;
        // Search upwards to find backend/gateway/src/EventPulse.Gateway/appsettings.json
        var dir = new DirectoryInfo(baseDir);
        string? appsettingsPath = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "gateway", "src", "EventPulse.Gateway", "appsettings.json");
            if (File.Exists(candidate))
            {
                appsettingsPath = candidate;
                break;
            }
            var candidate2 = Path.Combine(dir.FullName, "gateway", "src", "EventPulse.Gateway", "appsettings.json");
            if (File.Exists(candidate2))
            {
                appsettingsPath = candidate2;
                break;
            }
            dir = dir.Parent;
        }

        Assert.True(File.Exists(appsettingsPath), $"Gateway appsettings.json not found from {baseDir}");

        var json = File.ReadAllText(appsettingsPath!);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var routes = root.GetProperty("ReverseProxy").GetProperty("Routes");
        var clusters = root.GetProperty("ReverseProxy").GetProperty("Clusters");

        // 1. Verify identity-users-route exists and covers /api/users/{**catch-all}
        Assert.True(routes.TryGetProperty("identity-users-route", out var usersRoute), "identity-users-route missing");
        var matchPath = usersRoute.GetProperty("Match").GetProperty("Path").GetString();
        Assert.Equal("/api/users/{**catch-all}", matchPath);

        var clusterId = usersRoute.GetProperty("ClusterId").GetString();
        Assert.Equal("identity_cluster", clusterId);

        // 2. Verify identity_cluster destination points to IdentityService port 7101
        Assert.True(clusters.TryGetProperty("identity_cluster", out var identityCluster), "identity_cluster missing");
        var destination = identityCluster.GetProperty("Destinations").GetProperty("identity_service");
        var address = destination.GetProperty("Address").GetString();
        Assert.Equal("http://localhost:7101", address);

        // 3. Verify other key routes are intact
        Assert.True(routes.TryGetProperty("identity-auth-route", out _));
        Assert.True(routes.TryGetProperty("events-route", out _));
        Assert.True(routes.TryGetProperty("bookings-route", out _));
        Assert.True(routes.TryGetProperty("payments-route", out _));
    }
}
