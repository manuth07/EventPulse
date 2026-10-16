namespace EventPulse.EventService.Models;

/// <summary>
/// Persistent administrator notification generated when an organizer submits an event for review (EP-150 / EP-32).
/// Stores submission metadata and tracks read status and review progression.
/// </summary>
public class AdminNotification
{
    /// <summary>
    /// Primary key for the notification record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique identifier of the Kafka EventSubmitted integration event message.
    /// Used for idempotent persistence and deduplication.
    /// </summary>
    public Guid EventMessageId { get; set; }

    /// <summary>
    /// Identifier of the submitted domain Event entity requiring administrator review.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Snapshot title of the submitted event for display in notifications.
    /// </summary>
    public string EventTitle { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the organizer who submitted the event.
    /// </summary>
    public Guid OrganizerId { get; set; }

    /// <summary>
    /// Formatted human-readable notification message/title.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Frontend/UI navigation target URL or route for the administrator review interface.
    /// </summary>
    public string NavigationTarget { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the event submission occurred (UTC).
    /// </summary>
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this notification record was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the notification has been marked as read by an administrator.
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Timestamp when the notification was marked as read (UTC).
    /// </summary>
    public DateTimeOffset? ReadAtUtc { get; set; }

    /// <summary>
    /// Optional navigation/reference link back to the domain Event entity.
    /// </summary>
    public Event? Event { get; set; }
}
