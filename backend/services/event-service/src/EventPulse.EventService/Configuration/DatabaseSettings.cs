namespace EventPulse.EventService.Configuration;

/// <summary>
/// Configuration settings for database migration operations on application startup.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>
    /// If true, pending EF Core migrations are executed during application startup.
    /// Default is false. In Azure App Service, set Database__MigrateOnStartup = true.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = false;
}
