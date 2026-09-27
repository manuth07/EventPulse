using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class TicketGenerationService : ITicketGenerationService
{
    private readonly BookingDbContext _dbContext;
    private readonly ITicketCodeGenerator _ticketCodeGenerator;
    private readonly IValidationTokenGenerator _validationTokenGenerator;
    private readonly ILogger<TicketGenerationService> _logger;

    public TicketGenerationService(
        BookingDbContext dbContext,
        ITicketCodeGenerator ticketCodeGenerator,
        IValidationTokenGenerator validationTokenGenerator,
        ILogger<TicketGenerationService> logger)
    {
        _dbContext = dbContext;
        _ticketCodeGenerator = ticketCodeGenerator;
        _validationTokenGenerator = validationTokenGenerator;
        _logger = logger;
    }

    public async Task<TicketGenerationResult> GenerateTicketsForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating ticket generation for Booking {BookingId}", bookingId);

        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogWarning("Booking {BookingId} not found for ticket generation", bookingId);
            return TicketGenerationResult.NotFound(bookingId);
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            _logger.LogWarning("Cannot generate tickets for Booking {BookingId} with status {Status}. Only Confirmed bookings can generate tickets.",
                bookingId, booking.Status);
            return TicketGenerationResult.InvalidStatus(bookingId, booking.Status);
        }

        var existingTickets = await _dbContext.Tickets
            .Where(t => t.BookingId == bookingId)
            .ToListAsync(cancellationToken);

        var newTickets = new List<Ticket>();

        foreach (var item in booking.Items)
        {
            for (int seq = 1; seq <= item.Quantity; seq++)
            {
                var alreadyGenerated = existingTickets.Any(t => t.BookingItemId == item.Id && t.TicketSequence == seq);
                if (alreadyGenerated)
                {
                    continue;
                }

                var ticketCode = await _ticketCodeGenerator.GenerateUniqueTicketCodeAsync(cancellationToken);
                var validationToken = await _validationTokenGenerator.GenerateUniqueValidationTokenAsync(cancellationToken);

                var ticket = new Ticket
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    BookingItemId = item.Id,
                    TicketTypeId = item.TicketTypeId,
                    TicketName = item.TicketName,
                    TicketSequence = seq,
                    EventId = booking.EventId,
                    TicketCode = ticketCode,
                    ValidationToken = validationToken,
                    Status = TicketStatus.Valid,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                newTickets.Add(ticket);
            }
        }

        if (newTickets.Count > 0)
        {
            await _dbContext.Tickets.AddRangeAsync(newTickets, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated {NewCount} new ticket(s) for Booking {BookingId} (Total tickets: {TotalCount})",
                newTickets.Count, booking.Id, existingTickets.Count + newTickets.Count);
        }
        else
        {
            _logger.LogInformation("All {TotalCount} tickets for Booking {BookingId} are already generated; operation is idempotent.",
                existingTickets.Count, booking.Id);
        }

        var allTickets = existingTickets.Concat(newTickets).OrderBy(t => t.TicketSequence).ToList();
        return TicketGenerationResult.Success(booking.Id, allTickets, newTickets.Count);
    }
}
