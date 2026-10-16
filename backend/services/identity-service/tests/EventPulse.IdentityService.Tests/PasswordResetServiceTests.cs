using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class PasswordResetServiceTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly PasswordResetTokenService _tokenService;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly Mock<ILogger<PasswordResetService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(dbOptions);

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _tokenService = new PasswordResetTokenService();
        _emailSenderMock = new Mock<IEmailSender>();
        _loggerMock = new Mock<ILogger<PasswordResetService>>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "PasswordReset:TokenExpiryMinutes", "30" },
            { "PasswordReset:FrontendBaseUrl", "http://localhost:5173" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _sut = new PasswordResetService(
            _userManagerMock.Object,
            _dbContext,
            _tokenService,
            _emailSenderMock.Object,
            _configuration,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_RegisteredActiveUser_GeneratesHashedTokenAndPersists()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "john.doe@example.com",
            UserName = "john.doe@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("john.doe@example.com"))
            .ReturnsAsync(user);

        var result = await _sut.RequestPasswordResetAsync("john.doe@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", result.Message);
        Assert.NotNull(result.RawToken);

        var storedToken = await _dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == userId);
        Assert.NotNull(storedToken);
        Assert.Equal(_tokenService.HashToken(result.RawToken!), storedToken.TokenHash);
        Assert.True(storedToken.ExpiresAt > DateTime.UtcNow);
        Assert.Null(storedToken.UsedAt);

        _emailSenderMock.Verify(m => m.SendPasswordResetEmailAsync(
            "john.doe@example.com",
            It.Is<string>(url => url.Contains(result.RawToken!)),
            30), Times.Once);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_RegisteredUser_SendsEmailWithCorrectResetUrlAndExpiry()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "jane.smith@example.com",
            UserName = "jane.smith@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("jane.smith@example.com"))
            .ReturnsAsync(user);

        string? capturedUrl = null;
        int? capturedExpiry = null;
        string? capturedRecipient = null;

        _emailSenderMock.Setup(m => m.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Callback<string, string, int>((to, url, expiry) =>
            {
                capturedRecipient = to;
                capturedUrl = url;
                capturedExpiry = expiry;
            })
            .Returns(Task.CompletedTask);

        var result = await _sut.RequestPasswordResetAsync("jane.smith@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal("jane.smith@example.com", capturedRecipient);
        Assert.Equal(30, capturedExpiry);
        Assert.NotNull(capturedUrl);
        Assert.StartsWith("http://localhost:5173/reset-password?token=", capturedUrl);
        Assert.Contains(Uri.EscapeDataString(result.RawToken!), capturedUrl);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_CustomFrontendBaseUrl_UsesConfiguredBaseUrlInResetUrl()
    {
        var customSettings = new Dictionary<string, string?>
        {
            { "PasswordReset:TokenExpiryMinutes", "45" },
            { "PasswordReset:FrontendBaseUrl", "https://eventpulse.app" }
        };
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(customSettings)
            .Build();

        var customSut = new PasswordResetService(
            _userManagerMock.Object,
            _dbContext,
            _tokenService,
            _emailSenderMock.Object,
            customConfig,
            _loggerMock.Object);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "custom.url@example.com",
            UserName = "custom.url@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("custom.url@example.com"))
            .ReturnsAsync(user);

        string? capturedUrl = null;
        int? capturedExpiry = null;

        _emailSenderMock.Setup(m => m.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .Callback<string, string, int>((_, url, expiry) =>
            {
                capturedUrl = url;
                capturedExpiry = expiry;
            })
            .Returns(Task.CompletedTask);

        var result = await customSut.RequestPasswordResetAsync("custom.url@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal(45, capturedExpiry);
        Assert.NotNull(capturedUrl);
        Assert.StartsWith("https://eventpulse.app/reset-password?token=", capturedUrl);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_EmailSenderThrows_SafelyLogsAndReturnsGenericResponseWithoutExposingError()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "smtp.failure@example.com",
            UserName = "smtp.failure@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("smtp.failure@example.com"))
            .ReturnsAsync(user);

        _emailSenderMock.Setup(m => m.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()))
            .ThrowsAsync(new System.Net.Sockets.SocketException(10061));

        // Must not throw or bubble SMTP exception up to caller
        var result = await _sut.RequestPasswordResetAsync("smtp.failure@example.com");

        // Uniform generic response preserved
        Assert.True(result.Succeeded);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", result.Message);
        Assert.NotNull(result.RawToken);

        // Token was still persisted in case retry/resend or service recovers
        var storedToken = await _dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        Assert.NotNull(storedToken);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_UnknownEmail_ReturnsGenericResponseWithoutCreatingToken()
    {
        _userManagerMock.Setup(m => m.FindByEmailAsync("unknown@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _sut.RequestPasswordResetAsync("unknown@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", result.Message);
        Assert.Null(result.RawToken);

        var tokenCount = await _dbContext.PasswordResetTokens.CountAsync();
        Assert.Equal(0, tokenCount);

        _emailSenderMock.Verify(m => m.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_InactiveUser_ReturnsGenericResponseWithoutCreatingToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "disabled@example.com",
            UserName = "disabled@example.com",
            IsActive = false
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("disabled@example.com"))
            .ReturnsAsync(user);

        var result = await _sut.RequestPasswordResetAsync("disabled@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", result.Message);
        Assert.Null(result.RawToken);

        var tokenCount = await _dbContext.PasswordResetTokens.CountAsync();
        Assert.Equal(0, tokenCount);

        _emailSenderMock.Verify(m => m.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_MultipleRequests_InvalidatesPreviousTokens()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            UserName = "user@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(m => m.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        var firstRequest = await _sut.RequestPasswordResetAsync("user@example.com");
        var firstRawToken = firstRequest.RawToken!;
        var firstHash = _tokenService.HashToken(firstRawToken);

        var secondRequest = await _sut.RequestPasswordResetAsync("user@example.com");
        var secondRawToken = secondRequest.RawToken!;
        var secondHash = _tokenService.HashToken(secondRawToken);

        var tokens = await _dbContext.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);

        var firstStored = tokens.First(t => t.TokenHash == firstHash);
        var secondStored = tokens.First(t => t.TokenHash == secondHash);

        Assert.True(firstStored.ExpiresAt <= DateTime.UtcNow);
        Assert.True(secondStored.ExpiresAt > DateTime.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestPasswordResetAsync_NullOrWhitespaceEmail_ReturnsGenericResponse(string? email)
    {
        var result = await _sut.RequestPasswordResetAsync(email!);

        Assert.True(result.Succeeded);
        Assert.Equal("If an account exists for this email, a password reset link has been sent.", result.Message);
        Assert.Null(result.RawToken);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_ValidToken_ReturnsValidAndDoesNotConsumeToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "valid@example.com",
            UserName = "valid@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ValidateResetTokenAsync(rawToken);

        Assert.True(result.IsValid);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("The password reset token is valid.", result.Message);

        // Crucial security check: Token must NOT be consumed
        var reloaded = await _dbContext.PasswordResetTokens.FindAsync(tokenRecord.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.UsedAt);
        Assert.False(reloaded.IsUsed);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_TokenNotFound_ReturnsInvalidResponse()
    {
        var rawToken = _tokenService.GenerateRawToken();

        var result = await _sut.ValidateResetTokenAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_TOKEN", result.Code);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_ExpiredToken_ReturnsExpiredCode()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "expired@example.com",
            UserName = "expired@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ValidateResetTokenAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("EXPIRED_TOKEN", result.Code);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_AlreadyUsedToken_ReturnsAlreadyUsedCode()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "used@example.com",
            UserName = "used@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UsedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ValidateResetTokenAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("TOKEN_ALREADY_USED", result.Code);
        Assert.Contains("already been used", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateResetTokenAsync_InactiveUser_ReturnsInvalidResponse()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "inactive@example.com",
            UserName = "inactive@example.com",
            IsActive = false
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ValidateResetTokenAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_TOKEN", result.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateResetTokenAsync_NullOrWhitespaceToken_ReturnsInvalidResponse(string? token)
    {
        var result = await _sut.ValidateResetTokenAsync(token!);

        Assert.False(result.IsValid);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_TOKEN", result.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_SuccessfullyResetsPasswordAndConsumesToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "reset.success@example.com",
            UserName = "reset.success@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.RemovePasswordAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddPasswordAsync(user, "NewSecurePass123!")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        var request = new DTOs.ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "NewSecurePass123!",
            ConfirmPassword = "NewSecurePass123!"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.StatusCode);

        // Verify token was consumed
        var consumedToken = await _dbContext.PasswordResetTokens.FindAsync(tokenRecord.Id);
        Assert.NotNull(consumedToken);
        Assert.True(consumedToken.IsUsed);
        Assert.NotNull(consumedToken.UsedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_PasswordActuallyChanged_OldPasswordRejectedAndNewPasswordAccepted()
    {
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var oldPassword = "OldSecurePass123!";
        var newPassword = "NewSecurePass456!";

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "password.change@example.com",
            UserName = "password.change@example.com",
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, oldPassword);
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.RemovePasswordAsync(user)).ReturnsAsync(() =>
        {
            user.PasswordHash = null;
            return IdentityResult.Success;
        });
        _userManagerMock.Setup(m => m.AddPasswordAsync(user, newPassword)).ReturnsAsync(() =>
        {
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            return IdentityResult.Success;
        });
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync((ApplicationUser u, string pwd) =>
        {
            if (u.PasswordHash == null) return false;
            return passwordHasher.VerifyHashedPassword(u, u.PasswordHash, pwd) == PasswordVerificationResult.Success;
        });

        // Verify old password works before reset
        Assert.True(await _userManagerMock.Object.CheckPasswordAsync(user, oldPassword));

        var request = new DTOs.ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = newPassword,
            ConfirmPassword = newPassword
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.True(result.Succeeded);

        // Verify old password no longer works
        Assert.False(await _userManagerMock.Object.CheckPasswordAsync(user, oldPassword));

        // Verify new password works
        Assert.True(await _userManagerMock.Object.CheckPasswordAsync(user, newPassword));
    }

    [Fact]
    public async Task ResetPasswordAsync_ReusedToken_ReturnsAlreadyUsedCode()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "reused.token@example.com",
            UserName = "reused.token@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UsedAt = DateTime.UtcNow.AddMinutes(-1) // Already consumed
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var request = new DTOs.ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "NewSecurePass123!",
            ConfirmPassword = "NewSecurePass123!"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("TOKEN_ALREADY_USED", result.Code);
        Assert.Contains("already been used", result.Message, StringComparison.OrdinalIgnoreCase);

        _userManagerMock.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsExpiredCode()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "expired.token@example.com",
            UserName = "expired.token@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-35),
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        var request = new DTOs.ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "NewSecurePass123!",
            ConfirmPassword = "NewSecurePass123!"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("EXPIRED_TOKEN", result.Code);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);

        _userManagerMock.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ReturnsInvalidTokenCode()
    {
        var request = new DTOs.ResetPasswordRequest
        {
            Token = "non-existent-token",
            NewPassword = "NewSecurePass123!",
            ConfirmPassword = "NewSecurePass123!"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_TOKEN", result.Code);

        _userManagerMock.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_FailsValidationAndDoesNotConsumeToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "weak.password@example.com",
            UserName = "weak.password@example.com",
            IsActive = true
        };
        _dbContext.Users.Add(user);

        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var tokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };
        _dbContext.PasswordResetTokens.Add(tokenRecord);
        await _dbContext.SaveChangesAsync();

        // Simulate password validator rejecting weak password
        var mockValidator = new Mock<IPasswordValidator<ApplicationUser>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<UserManager<ApplicationUser>>(), user, "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password must require a non-alphanumeric character." }));

        _userManagerMock.Object.PasswordValidators.Add(mockValidator.Object);

        var request = new DTOs.ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "weak",
            ConfirmPassword = "weak"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_PASSWORD", result.Code);
        Assert.NotEmpty(result.Errors);

        // Crucial security check: Token must NOT be consumed if password validation failed
        var untouchedToken = await _dbContext.PasswordResetTokens.FindAsync(tokenRecord.Id);
        Assert.NotNull(untouchedToken);
        Assert.Null(untouchedToken.UsedAt);
        Assert.False(untouchedToken.IsUsed);
    }

    [Fact]
    public async Task ResetPasswordAsync_MismatchedConfirmPassword_ReturnsPasswordsDoNotMatch()
    {
        var request = new DTOs.ResetPasswordRequest
        {
            Token = "some-token",
            NewPassword = "SecurePassword123!",
            ConfirmPassword = "DifferentPassword456!"
        };

        var result = await _sut.ResetPasswordAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PASSWORDS_DO_NOT_MATCH", result.Code);

        _userManagerMock.Verify(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }
}
