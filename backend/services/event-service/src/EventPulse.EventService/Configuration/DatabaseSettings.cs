namespace EventPulse.EventService.Configuration;

/// <summary>
/// Configuration settings for database migration and seeding operations on application startup.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>
    /// If true, pending EF Core migrations are executed during application startup.
    /// Default is false. In Azure App Service, set Database__MigrateOnStartup = true.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = false;

    /// <summary>
    /// If true, initial demo and test event data is seeded during application startup.
    /// Default is false. Should remain false in production environments.
    /// In Azure App Service, set Database__SeedOnStartup = true if demo data is explicitly needed.
    /// </summary>
    public bool SeedOnStartup { get; set; } = false;
}
