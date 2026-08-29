using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Tests;

public class LoginServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<ILogger<LoginService>> _loggerMock;
    private readonly LoginService _sut;

    public LoginServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _loggerMock = new Mock<ILogger<LoginService>>();

        _sut = new LoginService(
            _userManagerMock.Object,
            _jwtTokenServiceMock.Object,
            _loggerMock.Object);
    }

    private static LoginRequest ValidRequest() => new()
    {
        Email = "nimal.test@example.com",
        Password = "SecurePass123!"
    };

    private static ApplicationUser ValidUser(bool emailConfirmed = true, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = "nimal.test@example.com",
        FirstName = "Nimal",
        LastName = "Perera",
        EmailConfirmed = emailConfirmed,
        IsActive = isActive
    };

    // AC: Invalid credentials are rejected — no account exists for the email
    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        var request = ValidRequest();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _sut.LoginAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CREDENTIALS", result.Code);
        Assert.Equal(401, result.StatusCode);
        _userManagerMock.Verify(
            m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never); // must never attempt password check if user doesn't exist
        _jwtTokenServiceMock.Verify(
            m => m.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()),
            Times.Never);
    }

    // AC: Invalid credentials are rejected — wrong password
    [Fact]
    public async Task LoginAsync_WhenPasswordIsWrong_ReturnsInvalidCredentials()
    {
        var request = ValidRequest();
        var user = ValidUser();
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(false);

        var result = await _sut.LoginAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CREDENTIALS", result.Code);
        Assert.Equal(401, result.StatusCode);
        _userManagerMock.Verify(
            m => m.GetRolesAsync(It.IsAny<ApplicationUser>()),
            Times.Never); // must not proceed to role lookup if password is wrong
    }

    // Not an explicit AC line-item, but a real branch in the code:
    // an unverified account must not be allowed to authenticate.
    [Fact]
    public async Task LoginAsync_WhenEmailNotConfirmed_ReturnsEmailNotVerified()
    {
        var request = ValidRequest();
        var user = ValidUser(emailConfirmed: false);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        var result = await _sut.LoginAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.Code);
        Assert.Equal(403, result.StatusCode);
        _jwtTokenServiceMock.Verify(
            m => m.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()),
            Times.Never);
    }

    // Not an explicit AC line-item, but a real branch in the code:
    // a disabled account must not be allowed to authenticate, even with a verified email.
    [Fact]
    public async Task LoginAsync_WhenAccountIsInactive_ReturnsAccountDisabled()
    {
        var request = ValidRequest();
        var user = ValidUser(emailConfirmed: true, isActive: false);
        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        var result = await _sut.LoginAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("ACCOUNT_DISABLED", result.Code);
        Assert.Equal(403, result.StatusCode);
        _jwtTokenServiceMock.Verify(
            m => m.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()),
            Times.Never);
    }

    // AC: Valid credentials allow access; authenticated session/token is created
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndUserDetails()
    {
        var request = ValidRequest();
        var user = ValidUser();
        var roles = new List<string> { "Customer" };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _jwtTokenServiceMock
            .Setup(m => m.GenerateToken(user, roles))
            .Returns(("fake-jwt-token", 3600));

        var result = await _sut.LoginAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("fake-jwt-token", result.Response!.AccessToken);
        Assert.Equal("Bearer", result.Response.TokenType);
        Assert.Equal(3600, result.Response.ExpiresIn);
        Assert.Equal(user.Id, result.Response.User.Id);
        Assert.Equal(user.Email, result.Response.User.Email);
        Assert.Equal(user.FirstName, result.Response.User.FirstName);
        Assert.Equal(user.LastName, result.Response.User.LastName);
        Assert.Equal(roles, result.Response.User.Roles);
        _jwtTokenServiceMock.Verify(
            m => m.GenerateToken(user, roles),
            Times.Once);
    }
    [Fact]
public async Task LoginAsync_WhenUserEmailIsNull_ReturnsEmptyStringForEmail()
{
    var request = ValidRequest();
    var user = ValidUser();
    user.Email = null; // exercise the ?? string.Empty branch
    var roles = new List<string> { "Customer" };

    _userManagerMock
        .Setup(m => m.FindByEmailAsync(request.Email))
        .ReturnsAsync(user);
    _userManagerMock
        .Setup(m => m.CheckPasswordAsync(user, request.Password))
        .ReturnsAsync(true);
    _userManagerMock
        .Setup(m => m.GetRolesAsync(user))
        .ReturnsAsync(roles);
    _jwtTokenServiceMock
        .Setup(m => m.GenerateToken(user, roles))
        .Returns(("fake-jwt-token", 3600));

    var result = await _sut.LoginAsync(request);

    Assert.True(result.Succeeded);
    Assert.Equal(string.Empty, result.Response!.User.Email);
}
}