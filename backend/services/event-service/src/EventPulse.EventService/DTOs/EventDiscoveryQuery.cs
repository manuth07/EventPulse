namespace EventPulse.EventService.DTOs;

/// <summary>
/// Query parameters for customer event discovery and search (EP-37 / US-17, extensible for US-18).
/// </summary>
public class EventDiscoveryQuery
{
    /// <summary>
    /// Free-text search term matched against Title, Category, Venue, and Description.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Optional canonical category filter (e.g., Music, Sports). Reserved for US-18.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional venue type filter (Indoor, Outdoor). Reserved for US-18.
    /// </summary>
    public string? VenueType { get; set; }
}
