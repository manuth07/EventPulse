namespace EventPulse.EventService.Models;

/// <summary>
/// Represents an Organizer's request to cancel an approved or published Event (EP-35 US-15).
/// The approved Event remains unchanged while this request is in Pending status.
/// In a future phase, an Administrator reviews and approves or rejects this cancellation.
/// </summary>
public class EventCancellationRequest
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key referencing the live Event being requested for cancellation.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Navigation property to the target Event.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Identifier of the Organizer who submitted the cancellation request.
    /// Derived from authenticated JWT claims.
    /// </summary>
    public Guid OrganizerId { get; set; }

    /// <summary>
    /// The mandatory explanation provided by the Organizer for cancelling the event.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle status of this cancellation request (Pending, Approved, Rejected).
    /// </summary>
    public EventCancellationRequestStatus Status { get; set; } = EventCancellationRequestStatus.Pending;

    /// <summary>
    /// Timestamp when this cancellation request was submitted (UTC).
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this request was reviewed by an Administrator (null while Pending).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Identifier of the Administrator who reviewed this request (null while Pending).
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>
    /// Administrator feedback, review notes, or rejection reason (null while Pending).
    /// </summary>
    public string? ReviewComment { get; set; }
}
