using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public TicketsController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(customerIdStr, out customerId);
    }

    /// <summary>
    /// Authoritative lookup for an individual ticket by ticket ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicketById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var ticket = await _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (ticket == null)
        {
            return NotFound(new { code = "TICKET_NOT_FOUND", message = "Ticket not found." });
        }

        if (ticket.Booking == null || (ticket.Booking.CustomerId != customerId && !User.IsInRole("Administrator")))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "You are not authorized to view this ticket." });
        }

        var dto = new CustomerTicketDto
        {
            TicketId = ticket.Id,
            TicketCode = ticket.TicketCode,
            BookingId = ticket.BookingId,
            BookingReference = ticket.Booking.BookingReference,
            EventId = ticket.EventId,
            TicketTypeId = ticket.TicketTypeId,
            TicketName = ticket.TicketName,
            TicketSequence = ticket.TicketSequence,
            Status = ticket.Status.ToString(),
            ValidationToken = ticket.ValidationToken,
            QrPayload = $"eventpulse-ticket:{ticket.ValidationToken}",
            CreatedAt = ticket.CreatedAt
        };

        return Ok(dto);
    }
}
