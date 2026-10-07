using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IBookingHistoryService
{
    Task<PagedResult<CustomerBookingHistoryDto>> GetCustomerBookingHistoryAsync(
        Guid customerId,
        BookingHistoryQueryParameters queryParams,
        CancellationToken ct = default);

    Task<CustomerBookingDetailResult> GetCustomerBookingDetailAsync(
        Guid bookingId,
        Guid customerId,
        bool isAdmin = false,
        CancellationToken ct = default);
}
