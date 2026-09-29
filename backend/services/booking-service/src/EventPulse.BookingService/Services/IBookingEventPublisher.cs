using EventPulse.BookingService.Events;

namespace EventPulse.BookingService.Services;

public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(BookingCreatedEvent @event, CancellationToken ct = default);
    Task PublishBookingCancelledAsync(BookingCancelledEvent @event, CancellationToken ct = default);
}
