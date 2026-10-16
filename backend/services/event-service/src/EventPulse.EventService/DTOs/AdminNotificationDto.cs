namespace EventPulse.EventService.DTOs;

/// <summary>
/// Data Transfer Object representing an administrator notification for event submission review (EP-150 / EP-32).
/// Contains live event review status and navigation details.
/// </summary>
public class AdminNotificationDto
{
    public Guid Id { get; set; }
    public Guid EventMessageId { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string NavigationTarget { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }

    /// <summary>
    /// Current authoritative review status of the underlying event ("Pending", "Approved", "Rejected", etc.).
    /// </summary>
    public string ReviewStatus { get; set; } = string.Empty;
}
