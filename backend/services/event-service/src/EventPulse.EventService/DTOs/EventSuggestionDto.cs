namespace EventPulse.EventService.DTOs;

/// <summary>
/// Lightweight autocomplete suggestion item for customer search (EP-37 / US-17).
/// Omits heavy description and blob binaries for high performance.
/// </summary>
public class EventSuggestionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? Venue { get; set; }

    public DateTime EventDate { get; set; }
}
