namespace EventPulse.BookingService.Services;

public class TicketTypeAvailability
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsSoldOut { get; set; }
}

/// <summary>
/// Fetches live ticket type availability from EventService so cart operations
/// can validate requested quantity without duplicating EventService's data.
/// </summary>
public interface IEventAvailabilityClient
{
    Task<TicketTypeAvailability?> GetTicketTypeAsync(Guid eventId, Guid ticketTypeId, CancellationToken cancellationToken = default);
}