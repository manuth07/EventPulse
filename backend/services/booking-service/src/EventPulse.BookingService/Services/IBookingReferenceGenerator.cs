namespace EventPulse.BookingService.Services;

public interface IBookingReferenceGenerator
{
    Task<string> GenerateUniqueReferenceAsync(CancellationToken cancellationToken = default);
}
