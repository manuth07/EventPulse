using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IOrganizerSalesAnalyticsService
{
    /// <summary>
    /// EP-384: Retrieves overall sales summary metrics for the authenticated organizer's events.
    /// Returns zeros if organizer has no sales. Supports optional date filtering.
    /// </summary>
    Task<OrganizerSalesSummaryDto> GetOrganizerSalesSummaryAsync(
        Guid organizerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-384: Retrieves sales analytics for a specific event owned by the authenticated organizer,
    /// including breakdown by ticket type. Verifies event ownership server-side.
    /// Returns null (404) if event does not exist or belongs to another organizer.
    /// </summary>
    Task<EventSalesAnalyticsDto?> GetEventSalesAnalyticsAsync(
        Guid organizerId,
        Guid eventId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-384: Retrieves paginated event-level sales summaries for all events owned by the organizer.
    /// Supports optional title search and date filtering.
    /// </summary>
    Task<PagedResult<EventSalesSummaryItemDto>> GetOrganizerEventsSalesListAsync(
        Guid organizerId,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
