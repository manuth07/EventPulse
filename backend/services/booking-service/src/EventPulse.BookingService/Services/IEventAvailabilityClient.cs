namespace EventPulse.BookingService.Services;

public class TicketTypeAvailability
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsSoldOut { get; set; }
}

public class EventSummaryInfo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? ImageUrl { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Fetches live ticket type availability and event summary from EventService so cart operations
/// can validate requested quantity and display event context without duplicating EventService's data.
/// </summary>
public interface IEventAvailabilityClient
{
    Task<TicketTypeAvailability?> GetTicketTypeAsync(Guid eventId, Guid ticketTypeId, CancellationToken cancellationToken = default);
    Task<EventSummaryInfo?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken = default);
}