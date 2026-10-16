namespace EventPulse.EventService.Models;

/// <summary>
/// Reusable query extensions and visibility specifications for Event entities.
/// </summary>
public static class EventQueryExtensions
{
    /// <summary>
    /// Restricts queries to customer-visible events only: Status == Published AND EventDate > current UTC time.
    /// Prevents Draft, Pending, Approved (pre-sale), Rejected, Cancelled, and past/concluded events from leaking to public visitors.
    /// </summary>
    public static IQueryable<Event> WhereCustomerVisible(this IQueryable<Event> query, DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        return query.Where(e => e.Status == EventStatus.Published && e.EventDate > now);
    }
}
