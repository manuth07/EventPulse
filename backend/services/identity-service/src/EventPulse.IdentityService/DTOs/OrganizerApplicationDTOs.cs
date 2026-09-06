using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Payload submitted by an authenticated Customer requesting Organizer status.
/// All sensitive/system fields (Id, UserId, Status, Review fields) are server-controlled.
/// </summary>
public class CreateOrganizerApplicationRequest
{
    [Required(ErrorMessage = "Organizer name is required.")]
    [StringLength(200, ErrorMessage = "Organizer name cannot exceed 200 characters.")]
    public string OrganizerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Organizer type is required.")]
    public string OrganizerType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact number is required.")]
    [StringLength(50, MinimumLength = 7, ErrorMessage = "Contact number must be between 7 and 50 characters.")]
    public string ContactNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Website URL cannot exceed 500 characters.")]
    public string? Website { get; set; }
}

/// <summary>
/// Safe response DTO returned to the applicant.
/// Does not expose internal reviewer GUIDs or EF entities directly.
/// </summary>
public class OrganizerApplicationDto
{
    public Guid Id { get; set; }

    public string OrganizerName { get; set; } = string.Empty;

    public string OrganizerType { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Website { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }
}
