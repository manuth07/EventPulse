namespace EventPulse.EventService.Models;

/// <summary>
/// Reusable query extensions and visibility specifications for Event entities.
/// </summary>
public static class EventQueryExtensions
{
    /// <summary>
    /// Restricts queries to customer-visible events only (Status == Published).
    /// Prevents Draft, Pending, Approved (pre-sale), Rejected, and Cancelled events from leaking to public visitors.
    /// </summary>
    public static IQueryable<Event> WhereCustomerVisible(this IQueryable<Event> query)
    {
        return query.Where(e => e.Status == EventStatus.Published);
    }
}
