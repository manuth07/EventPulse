using Microsoft.AspNetCore.Mvc;
using Moq;
using EventPulse.IdentityService.Controllers;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Services;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class PasswordResetControllerTests
{
    private readonly Mock<IRegistrationService> _registrationServiceMock = new();
    private readonly Mock<ILoginService> _loginServiceMock = new();
    private readonly Mock<IGoogleAuthService> _googleAuthServiceMock = new();
    private readonly Mock<ISetPasswordService> _setPasswordServiceMock = new();
    private readonly Mock<IPasswordResetService> _passwordResetServiceMock = new();

    private AuthController CreateController()
    {
        return new AuthController(
            _registrationServiceMock.Object,
            _loginServiceMock.Object,
            _googleAuthServiceMock.Object,
            _setPasswordServiceMock.Object,
            _passwordResetServiceMock.Object);
    }

    [Fact]
    public async Task ForgotPassword_RegisteredEmail_Returns200OkWithGenericResponse()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.RequestPasswordResetAsync("registered@example.com"))
            .ReturnsAsync(RequestPasswordResetResult.GenericResponse("sample_raw_token"));

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "registered@example.com" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ForgotPasswordResponse>(okResult.Value);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", response.Message);
    }

    [Fact]
    public async Task ForgotPassword_UnregisteredEmail_Returns200OkWithIdenticalGenericResponse()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.RequestPasswordResetAsync("unregistered@example.com"))
            .ReturnsAsync(RequestPasswordResetResult.GenericResponse(null));

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "unregistered@example.com" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ForgotPasswordResponse>(okResult.Value);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", response.Message);
    }

    [Fact]
    public async Task ForgotPassword_InvalidModelState_Returns400BadRequest()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Email", "Enter a valid email address.");

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "invalid-email" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _passwordResetServiceMock.Verify(s => s.RequestPasswordResetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidateResetTokenPost_ValidToken_Returns200OkWithIsValidTrue()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.ValidateResetTokenAsync("valid-token"))
            .ReturnsAsync(ValidateResetTokenResult.Valid("The password reset token is valid."));

        var result = await controller.ValidateResetToken(new ValidateResetTokenRequest { Token = "valid-token" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateResetTokenResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal("The password reset token is valid.", response.Message);
    }

    [Fact]
    public async Task ValidateResetTokenPost_InvalidOrExpiredToken_Returns400BadRequestWithIsValidFalse()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.ValidateResetTokenAsync("invalid-token"))
            .ReturnsAsync(ValidateResetTokenResult.Invalid("INVALID_TOKEN", "The password reset link is invalid or has expired."));

        var result = await controller.ValidateResetToken(new ValidateResetTokenRequest { Token = "invalid-token" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusResult.StatusCode);
        var response = Assert.IsType<ValidateResetTokenResponse>(statusResult.Value);
        Assert.False(response.IsValid);
        Assert.Equal("INVALID_TOKEN", response.Code);
    }

    [Fact]
    public async Task ValidateResetTokenPost_AlreadyUsedToken_Returns400BadRequestWithAlreadyUsedCode()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.ValidateResetTokenAsync("used-token"))
            .ReturnsAsync(ValidateResetTokenResult.AlreadyUsed());

        var result = await controller.ValidateResetToken(new ValidateResetTokenRequest { Token = "used-token" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusResult.StatusCode);
        var response = Assert.IsType<ValidateResetTokenResponse>(statusResult.Value);
        Assert.False(response.IsValid);
        Assert.Equal("TOKEN_ALREADY_USED", response.Code);
    }

    [Fact]
    public async Task ValidateResetTokenPost_InvalidModelState_Returns400BadRequest()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Token", "Reset token is required.");

        var result = await controller.ValidateResetToken(new ValidateResetTokenRequest { Token = "" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _passwordResetServiceMock.Verify(s => s.ValidateResetTokenAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ValidateResetTokenGet_ValidTokenQuery_Returns200OkWithIsValidTrue()
    {
        var controller = CreateController();
        _passwordResetServiceMock
            .Setup(s => s.ValidateResetTokenAsync("valid-token"))
            .ReturnsAsync(ValidateResetTokenResult.Valid());

        var result = await controller.ValidateResetTokenGet("valid-token");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateResetTokenResponse>(okResult.Value);
        Assert.True(response.IsValid);
    }

    [Fact]
    public async Task ValidateResetTokenGet_MissingToken_Returns400BadRequest()
    {
        var controller = CreateController();

        var result = await controller.ValidateResetTokenGet("");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ValidateResetTokenResponse>(badRequest.Value);
        Assert.False(response.IsValid);
        Assert.Equal("INVALID_REQUEST", response.Code);
    }
}
