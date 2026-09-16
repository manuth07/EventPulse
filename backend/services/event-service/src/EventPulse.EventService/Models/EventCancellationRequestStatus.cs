namespace EventPulse.EventService.Models;

/// <summary>
/// Lifecycle status of an Event Cancellation Request submitted by an Organizer (EP-35 US-15).
/// </summary>
public enum EventCancellationRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
