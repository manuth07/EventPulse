namespace EventPulse.BookingService.Services;

public class TicketTypeAvailability
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public int RemainingQuantity { get; set; }
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

public class ReserveInventoryItemDto
{
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
}

public class ClientInventoryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    public static ClientInventoryResult Ok() => new() { Success = true, StatusCode = 200 };
    public static ClientInventoryResult Fail(string message, int code = 400) => new() { Success = false, ErrorMessage = message, StatusCode = code };
}

/// <summary>
/// EP-401: Fetches live ticket type availability and coordinates inventory holds,
/// commits, releases, and cancellations with EventService.
/// </summary>
public interface IEventAvailabilityClient
{
    Task<TicketTypeAvailability?> GetTicketTypeAsync(Guid eventId, Guid ticketTypeId, CancellationToken cancellationToken = default);
    Task<EventSummaryInfo?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<ClientInventoryResult> ReserveInventoryAsync(
        Guid eventId,
        Guid bookingId,
        IEnumerable<ReserveInventoryItemDto> items,
        int holdDurationMinutes = 15,
        CancellationToken cancellationToken = default);

    Task<ClientInventoryResult> CommitInventoryAsync(
        Guid eventId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<ClientInventoryResult> ReleaseInventoryAsync(
        Guid eventId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<ClientInventoryResult> RestoreInventoryAsync(
        Guid eventId,
        Guid bookingId,
        string cancellationKey,
        IEnumerable<ReserveInventoryItemDto> items,
        Guid? ticketId = null,
        CancellationToken cancellationToken = default);
}