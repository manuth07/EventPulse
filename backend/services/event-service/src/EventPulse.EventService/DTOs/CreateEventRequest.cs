using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Multipart/form-data request for an Organizer to submit a new event with a poster image.
/// Replaces SubmitEventRequest for the POST /api/events endpoint.
/// The submitting Organizer's identity is derived from the authenticated JWT — not this body.
/// Status, OrganizerUserId, and ImageBlobName are server-controlled and must NOT be provided.
/// </summary>
public class CreateEventRequest
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

    /// <summary>
    /// Event poster image. Required for new submissions.
    /// Accepted: JPEG, PNG, WebP. Maximum: 5 MB.
    /// </summary>
    [Required]
    public IFormFile Image { get; set; } = null!;
}
