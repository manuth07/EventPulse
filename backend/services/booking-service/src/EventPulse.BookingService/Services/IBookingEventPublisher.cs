using EventPulse.BookingService.Events;
using EventPulse.Contracts.Kafka.Events;

namespace EventPulse.BookingService.Services;

public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(BookingCreatedEvent @event, CancellationToken ct = default);
    Task PublishBookingCancelledAsync(BookingCancelledEvent @event, CancellationToken ct = default);
    Task PublishTicketCancelledAsync(TicketCancelledEvent @event, CancellationToken ct = default);
    Task PublishBookingRefundRequestedAsync(BookingRefundRequestedEvent @event, CancellationToken ct = default);
}

