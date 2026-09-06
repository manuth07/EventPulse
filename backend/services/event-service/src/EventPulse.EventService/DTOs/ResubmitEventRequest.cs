using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Multipart/form-data request for an Organizer to edit and resubmit a Rejected event.
/// Organizer identity is derived from the authenticated JWT.
/// If Image is provided, it replaces the current poster; if null, the existing poster is retained.
/// </summary>
public class ResubmitEventRequest
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
    /// Optional replacement event poster image.
    /// If omitted, the existing poster is preserved.
    /// Accepted: JPEG, PNG, WebP. Maximum: 5 MB.
    /// </summary>
    public IFormFile? Image { get; set; }
}
