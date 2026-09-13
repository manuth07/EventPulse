using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Multipart/form-data request for an Organizer to submit proposed updates to an approved/published event.
/// The submitting Organizer's identity is derived from the authenticated JWT.
/// Submitting this request creates an EventUpdateRequest and does NOT modify the live Event.
/// If replacement Image or CoverImage are supplied, they are uploaded and staged on the request;
/// if omitted, the current event's existing images are retained.
/// </summary>
public class SubmitEventUpdateRequest
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

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(50)]
    public string? VenueType { get; set; }

    /// <summary>
    /// Optional replacement event poster image (portrait).
    /// If omitted, the current approved poster is retained.
    /// Accepted: JPEG, PNG, WebP. Maximum: 5 MB.
    /// </summary>
    public IFormFile? Image { get; set; }

    /// <summary>
    /// Optional replacement wide event cover/banner image.
    /// If omitted, the current approved cover banner is retained.
    /// Accepted: JPEG, PNG, WebP. Maximum: 5 MB.
    /// </summary>
    public IFormFile? CoverImage { get; set; }
}
