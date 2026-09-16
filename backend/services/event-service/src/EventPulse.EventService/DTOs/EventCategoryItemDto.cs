namespace EventPulse.EventService.DTOs;

/// <summary>
/// DTO representing an event category option for dropdowns and filtering (EP-36 / US-16).
/// </summary>
public class EventCategoryItemDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
