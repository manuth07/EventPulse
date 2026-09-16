using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Request body for an Organizer to submit a new event.
/// The submitting Organizer's identity is derived from the authenticated JWT — not this body.
/// </summary>
public class SubmitEventRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Venue { get; set; } = string.Empty;

    [Required]
    public DateTime EventDate { get; set; }

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
}
