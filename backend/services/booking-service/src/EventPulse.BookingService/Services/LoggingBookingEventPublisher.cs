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
}
