using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.Events;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventClient;
    private readonly IBookingReferenceGenerator _referenceGenerator;
    private readonly IBookingEventPublisher _eventPublisher;
    private readonly ILogger<BookingsController> _logger;
    private readonly ICartService _cartService;

    public BookingsController(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventClient,
        IBookingReferenceGenerator referenceGenerator,
        IBookingEventPublisher eventPublisher,
        ILogger<BookingsController> logger,
        ICartService cartService)
    {
        _dbContext = dbContext;
        _eventClient = eventClient;
        _referenceGenerator = referenceGenerator;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _cartService = cartService;
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(customerIdStr, out customerId);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        // Validate event and tickets
        var eventSummary = await _eventClient.GetEventSummaryAsync(request.EventId, cancellationToken);
        if (eventSummary == null)
        {
            return BadRequest(new { code = "EVENT_NOT_FOUND", message = "The specified event could not be found." });
        }

        var bookingItems = new List<BookingItem>();
        decimal totalAmount = 0;
        var eventPayloadItems = new List<BookingItemDto>();

        foreach (var reqItem in request.Items)
        {
            var ticketInfo = await _eventClient.GetTicketTypeAsync(request.EventId, reqItem.TicketTypeId, cancellationToken);
            if (ticketInfo == null)
            {
                return BadRequest(new { code = "TICKET_TYPE_NOT_FOUND", message = $"Ticket type {reqItem.TicketTypeId} not found for this event." });
            }

            if (ticketInfo.IsSoldOut || ticketInfo.AvailableQuantity < reqItem.Quantity)
            {
                return BadRequest(new { code = "INSUFFICIENT_CAPACITY", message = $"Not enough tickets available for {ticketInfo.Name}." });
            }

            var subtotal = ticketInfo.Price * reqItem.Quantity;
            totalAmount += subtotal;

            bookingItems.Add(new BookingItem
            {
                TicketTypeId = ticketInfo.Id,
                TicketName = ticketInfo.Name,
                Quantity = reqItem.Quantity,
                UnitPrice = ticketInfo.Price,
                Subtotal = subtotal
            });

            eventPayloadItems.Add(new BookingItemDto
            {
                TicketTypeId = ticketInfo.Id,
                TicketName = ticketInfo.Name,
                Quantity = reqItem.Quantity,
                UnitPrice = ticketInfo.Price,
                Subtotal = subtotal
            });
        }

        // Generate Booking Reference
        var bookingReference = await _referenceGenerator.GenerateUniqueReferenceAsync(cancellationToken);

        var booking = new Booking
        {
            BookingReference = bookingReference,
            CustomerId = customerId,
            EventId = request.EventId,
            TotalAmount = totalAmount,
            Status = BookingStatus.PendingPayment,
            Items = bookingItems
        };

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Optional cart integration: clear cart if it exists for this user and event
            try
            {
                var cart = await _cartService.GetActiveCartAsync(customerId, cancellationToken);
                if (cart != null && cart.EventId == request.EventId)
                {
                    await _cartService.CompleteActiveCartAsync(customerId, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to complete cart for Customer {CustomerId}, Event {EventId} during booking creation.", customerId, request.EventId);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to save booking for Customer {CustomerId}, Event {EventId}", customerId, request.EventId);
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = "An error occurred while saving the booking." });
        }

        // Publish Event
        var bookingCreatedEvent = new BookingCreatedEvent
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            EventId = booking.EventId,
            TotalAmount = booking.TotalAmount,
            Items = eventPayloadItems,
            CreatedAt = booking.CreatedAt
        };

        await _eventPublisher.PublishBookingCreatedAsync(bookingCreatedEvent, cancellationToken);

        // Return Response
        var responseDto = new BookingResponseDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            EventId = booking.EventId,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status.ToString(),
            Items = booking.Items.Select(i => new BookingItemResponseDto
            {
                TicketTypeId = i.TicketTypeId,
                TicketName = i.TicketName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList()
        };

        return CreatedAtAction(nameof(CreateBooking), new { id = booking.Id }, responseDto);
    }
}
