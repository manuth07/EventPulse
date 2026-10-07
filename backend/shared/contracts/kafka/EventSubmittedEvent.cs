namespace EventPulse.Contracts.Kafka;

/// <summary>
/// Integration event emitted when an organizer successfully submits an event requiring administrator review (EP-146 / EP-32).
/// Contains only the essential submission identifiers and metadata required for notification delivery and idempotent processing.
/// </summary>
public class EventSubmittedEvent
{
    /// <summary>
    /// Unique identifier for this Kafka message instance. Used for message deduplication and idempotent consumer processing.
    /// </summary>
    public Guid EventMessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Contract schema version for forward/backward compatibility.
    /// </summary>
    public int EventVersion { get; set; } = 1;

    /// <summary>
    /// Timestamp when the event submission occurred (UTC).
    /// </summary>
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The unique identifier of the submitted domain Event entity.
    /// Used by administrators to navigate to the review page.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// The title of the submitted event for display in the administrator notification.
    /// </summary>
    public string EventTitle { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of the organizer who submitted the event.
    /// </summary>
    public Guid OrganizerId { get; set; }
}
