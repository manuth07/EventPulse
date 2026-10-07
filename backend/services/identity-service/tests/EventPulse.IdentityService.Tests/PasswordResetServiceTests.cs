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
        _loggerMock = new Mock<ILogger<PasswordResetService>>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "PasswordReset:TokenExpiryMinutes", "30" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _sut = new PasswordResetService(
            _userManagerMock.Object,
            _dbContext,
            _tokenService,
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
}
