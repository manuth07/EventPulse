using EventPulse.EventService.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventPulse.EventService.Tests;

public class DatabaseSettingsTests
{
    [Fact]
    public void DefaultSettings_ShouldHaveMigrateDisabled()
    {
        // Arrange & Act
        var settings = new DatabaseSettings();

        // Assert
        Assert.False(settings.MigrateOnStartup);
    }

    [Fact]
    public void ConfigurationBinding_WhenMigrateIsTrue_ShouldBindCorrectly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:MigrateOnStartup", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var settings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.MigrateOnStartup);
    }

    [Fact]
    public void ConfigurationBinding_WhenMigrateIsFalse_ShouldBindCorrectly()
    {
        // Arrange (simulates Azure App Service: Database__MigrateOnStartup=false)
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:MigrateOnStartup", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var settings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.MigrateOnStartup);
    }

    [Fact]
    public void ConfigurationBinding_WhenMissingSection_ShouldFallbackToDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var settings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? new DatabaseSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.MigrateOnStartup);
    }
}
