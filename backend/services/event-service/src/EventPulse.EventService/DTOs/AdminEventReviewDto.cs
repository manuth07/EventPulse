namespace EventPulse.EventService.DTOs;

/// <summary>
/// Focused DTO for Administrator review of an event submission.
/// Exposes event details, poster URL, submission date, and safe metadata.
/// </summary>
public class AdminEventReviewDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public Guid OrganizerId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
}
