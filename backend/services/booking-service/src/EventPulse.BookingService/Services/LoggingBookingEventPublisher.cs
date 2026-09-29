using System.Text.Json;
using EventPulse.BookingService.Events;

namespace EventPulse.BookingService.Services;

public class LoggingBookingEventPublisher : IBookingEventPublisher
{
    private readonly ILogger<LoggingBookingEventPublisher> _logger;

    public LoggingBookingEventPublisher(ILogger<LoggingBookingEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishBookingCreatedAsync(BookingCreatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing BookingCreatedEvent for Booking {BookingId}. Payload: {Payload}", 
            @event.BookingId, JsonSerializer.Serialize(@event));
        
        return Task.CompletedTask;
    }

    public Task PublishBookingCancelledAsync(BookingCancelledEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing BookingCancelledEvent for Booking {BookingId}. Payload: {Payload}", 
            @event.BookingId, JsonSerializer.Serialize(@event));
        
        return Task.CompletedTask;
    }

    public Task PublishTicketCancelledAsync(TicketCancelledEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing TicketCancelledEvent for Ticket {TicketId} (Booking {BookingId}). Payload: {Payload}", 
            @event.TicketId, @event.BookingId, JsonSerializer.Serialize(@event));
        
        return Task.CompletedTask;
    }
}
