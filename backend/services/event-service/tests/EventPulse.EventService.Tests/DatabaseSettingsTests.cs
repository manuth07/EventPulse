using EventPulse.EventService.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventPulse.EventService.Tests;

public class DatabaseSettingsTests
{
    [Fact]
    public void DefaultSettings_ShouldHaveMigrateAndSeedDisabled()
    {
        // Arrange & Act
        var settings = new DatabaseSettings();

        // Assert
        Assert.False(settings.MigrateOnStartup);
        Assert.False(settings.SeedOnStartup);
    }

    [Fact]
    public void ConfigurationBinding_WhenBothTrue_ShouldBindCorrectly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:MigrateOnStartup", "true" },
            { "Database:SeedOnStartup", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var settings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.MigrateOnStartup);
        Assert.True(settings.SeedOnStartup);
    }

    [Fact]
    public void ConfigurationBinding_WhenOnlyMigrateIsTrue_ShouldBindCorrectly()
    {
        // Arrange (simulates Azure App Service: Database__MigrateOnStartup=true, Database__SeedOnStartup=false)
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:MigrateOnStartup", "true" },
            { "Database:SeedOnStartup", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var settings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.MigrateOnStartup);
        Assert.False(settings.SeedOnStartup);
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
        Assert.False(settings.SeedOnStartup);
    }
}
