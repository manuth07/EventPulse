using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class PasswordResetTokenTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void NewToken_DefaultValues_AreCorrectlyInitialized()
    {
        var before = DateTime.UtcNow;
        var token = new PasswordResetToken();
        var after = DateTime.UtcNow;

        Assert.Equal(Guid.Empty, token.Id);
        Assert.Equal(Guid.Empty, token.UserId);
        Assert.Empty(token.TokenHash);
        Assert.Null(token.UsedAt);
        Assert.False(token.IsUsed);
        Assert.InRange(token.CreatedAt, before, after);
    }

    [Fact]
    public void IsUsed_WhenUsedAtIsNull_ReturnsFalse()
    {
        var token = new PasswordResetToken { UsedAt = null };

        Assert.False(token.IsUsed);
    }

    [Fact]
    public void IsUsed_WhenUsedAtIsSet_ReturnsTrue()
    {
        var token = new PasswordResetToken { UsedAt = DateTime.UtcNow };

        Assert.True(token.IsUsed);
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeIsBeforeExpiry_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            ExpiresAt = now.AddMinutes(15)
        };

        Assert.False(token.IsExpired(now));
    }

    [Fact]
    public void IsExpired_WhenCurrentTimeIsAtOrAfterExpiry_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            ExpiresAt = now.AddMinutes(-1)
        };

        Assert.True(token.IsExpired(now));
        Assert.True(token.IsExpired(token.ExpiresAt));
    }

    [Fact]
    public void IsActive_WhenUnusedAndNotExpired_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            ExpiresAt = now.AddMinutes(30),
            UsedAt = null
        };

        Assert.True(token.IsActive(now));
    }

    [Fact]
    public void IsActive_WhenUsed_ReturnsFalseEvenIfNotExpired()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            ExpiresAt = now.AddMinutes(30),
            UsedAt = now.AddMinutes(-5)
        };

        Assert.False(token.IsActive(now));
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalseEvenIfUnused()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            ExpiresAt = now.AddMinutes(-10),
            UsedAt = null
        };

        Assert.False(token.IsActive(now));
    }

    [Fact]
    public void MarkAsUsed_SetsUsedAtTimestampAndMarksAsUsed()
    {
        var token = new PasswordResetToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        var usedTime = DateTime.UtcNow;
        token.MarkAsUsed(usedTime);

        Assert.True(token.IsUsed);
        Assert.Equal(usedTime, token.UsedAt);
    }

    [Fact]
    public void MarkAsUsed_WhenAlreadyUsed_ThrowsInvalidOperationException()
    {
        var token = new PasswordResetToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            UsedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => token.MarkAsUsed());
        Assert.Contains("already been used", ex.Message);
    }

    [Fact]
    public async Task ApplicationDbContext_CanPersistAndRetrievePasswordResetToken()
    {
        using var context = CreateContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "reset.user@example.com",
            UserName = "reset.user@example.com",
            FirstName = "Alice",
            LastName = "Silva"
        };
        context.Users.Add(user);

        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "a3c5e8f01234567890abcdef1234567890abcdef1234567890abcdef12345678",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow
        };
        context.PasswordResetTokens.Add(token);
        await context.SaveChangesAsync();

        var retrieved = await context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == token.TokenHash);

        Assert.NotNull(retrieved);
        Assert.Equal(token.Id, retrieved.Id);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.Equal("reset.user@example.com", retrieved.User.Email);
        Assert.False(retrieved.IsUsed);
    }

    [Fact]
    public async Task ApplicationUser_HasNavigationCollection_PasswordResetTokens()
    {
        using var context = CreateContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "nav.user@example.com",
            UserName = "nav.user@example.com",
            FirstName = "Bob",
            LastName = "Perera"
        };

        var token1 = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash_token_1",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow
        };

        var token2 = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash_token_2",
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordResetTokens.Add(token1);
        user.PasswordResetTokens.Add(token2);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userFromDb = await context.Users
            .Include(u => u.PasswordResetTokens)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        Assert.NotNull(userFromDb);
        Assert.Equal(2, userFromDb.PasswordResetTokens.Count);
    }
}
