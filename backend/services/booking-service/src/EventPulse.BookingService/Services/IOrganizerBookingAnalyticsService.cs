using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IOrganizerBookingAnalyticsService
{
    /// <summary>
    /// EP-389: Retrieves overall booking summary metrics for the authenticated organizer's events.
    /// Returns zeros if the organizer has no bookings. Supports optional date filtering.
    /// </summary>
    Task<OrganizerBookingSummaryDto> GetOrganizerBookingSummaryAsync(
        Guid organizerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-389: Retrieves booking analytics for a specific event owned by the authenticated organizer,
    /// including breakdown by ticket type, cancellation rate, and remaining capacity.
    /// Verifies event ownership server-side. Returns null (404) if event does not exist or belongs to another organizer.
    /// </summary>
    Task<EventBookingAnalyticsDto?> GetEventBookingAnalyticsAsync(
        Guid organizerId,
        Guid eventId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-389: Retrieves paginated event-level booking summaries for all events owned by the organizer.
    /// Supports optional title search, date filtering, and pagination.
    /// </summary>
    Task<PagedResult<EventBookingSummaryItemDto>> GetOrganizerEventsBookingListAsync(
        Guid organizerId,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
