using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IBookingHistoryService
{
    Task<PagedResult<CustomerBookingHistoryDto>> GetCustomerBookingHistoryAsync(
        Guid customerId,
        BookingHistoryQueryParameters queryParams,
        CancellationToken ct = default);
}
