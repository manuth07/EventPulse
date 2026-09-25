using EventPulse.BookingService.Events;

namespace EventPulse.BookingService.Services;

public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(BookingCreatedEvent @event, CancellationToken ct = default);
}
