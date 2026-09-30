using EventPulse.BookingService.Data;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.BackgroundServices;

public class ExpiredBookingCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredBookingCleanupWorker> _logger;

    public ExpiredBookingCleanupWorker(IServiceProvider serviceProvider, ILogger<ExpiredBookingCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredBookingCleanupWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupExpiredBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while cleaning up expired bookings.");
            }
        }
    }

    private async Task CleanupExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IBookingEventPublisher>();

        var expiredBookings = await dbContext.Bookings
            .Include(b => b.Items)
            .Where(b => b.Status == BookingStatus.PendingPayment && b.ExpiresAt <= DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        if (expiredBookings.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} expired bookings to clean up.", expiredBookings.Count);

        foreach (var booking in expiredBookings)
        {
            booking.Status = BookingStatus.PaymentFailed; // Or BookingStatus.Cancelled if you prefer, but the model has PaymentFailed and Confirmed/PendingPayment

            var cancelledEvent = new BookingCancelledEvent
            {
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                CustomerId = booking.CustomerId,
                EventId = booking.EventId,
                CancelledAt = DateTimeOffset.UtcNow,
                ReleasedTickets = booking.Items.Select(i => new CancelledTicketItemDto(i.TicketTypeId, i.Quantity)).ToList(),
                Items = booking.Items.Select(i => new BookingItemDto
                {
                    TicketTypeId = i.TicketTypeId,
                    TicketName = i.TicketName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal
                }).ToList()
            };

            await eventPublisher.PublishBookingCancelledAsync(cancelledEvent, cancellationToken);
            _logger.LogInformation("Cancelled expired booking {BookingId}.", booking.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully cleaned up expired bookings.");
    }
}
