namespace EventPulse.EventService.Models;

/// <summary>
/// Lifecycle status of an Event Update Request submitted by an Organizer.
/// </summary>
public enum EventUpdateRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
