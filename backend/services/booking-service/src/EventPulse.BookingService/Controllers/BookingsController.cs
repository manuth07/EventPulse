using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly IBookingHistoryService _bookingHistoryService;
    private readonly IBookingCancellationService _cancellationService;

    public BookingsController(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventClient,
        IBookingReferenceGenerator referenceGenerator,
        IBookingEventPublisher eventPublisher,
        ILogger<BookingsController> logger,
        ICartService cartService,
        IBookingHistoryService bookingHistoryService,
        IBookingCancellationService cancellationService)
    {
        _dbContext = dbContext;
        _eventClient = eventClient;
        _referenceGenerator = referenceGenerator;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _cartService = cartService;
        _bookingHistoryService = bookingHistoryService;
        _cancellationService = cancellationService;
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(customerIdStr, out customerId);
    }

    /// <summary>
    /// Retrieves the authenticated customer's paginated booking history (US-27 / EP-47).
    /// </summary>
    [HttpGet("my-bookings")]
    [ProducesResponseType(typeof(PagedResult<CustomerBookingHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] BookingHistoryQueryParameters queryParams,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid or missing token identity." });
        }

        var result = await _bookingHistoryService.GetCustomerBookingHistoryAsync(customerId, queryParams, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Authoritative booking summary lookup for PaymentService and internal microservices.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetBookingSummary([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var booking = await _dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (booking == null)
        {
            return NotFound(new { code = "BOOKING_NOT_FOUND", message = "Booking not found." });
        }

        if (booking.CustomerId != customerId && !User.IsInRole("Administrator"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "You are not authorized to view this booking." });
        }

        return Ok(new BookingSummaryDto(
            booking.Id,
            booking.BookingReference,
            booking.CustomerId,
            booking.TotalAmount,
            booking.Status.ToString(),
            booking.ExpiresAt
        ));
    }

    /// <summary>
    /// Authoritative customer ticket retrieval for a booking.
    /// </summary>
    [HttpGet("{id:guid}/tickets")]
    public async Task<IActionResult> GetBookingTickets([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var booking = await _dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (booking == null)
        {
            return NotFound(new { code = "BOOKING_NOT_FOUND", message = "Booking not found." });
        }

        if (booking.CustomerId != customerId && !User.IsInRole("Administrator"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "FORBIDDEN", message = "You are not authorized to view tickets for this booking." });
        }

        var ticketDtos = booking.Tickets
            .OrderBy(t => t.TicketSequence)
            .Select(t => new CustomerTicketDto
            {
                TicketId = t.Id,
                TicketCode = t.TicketCode,
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                EventId = booking.EventId,
                TicketTypeId = t.TicketTypeId,
                TicketName = t.TicketName,
                TicketSequence = t.TicketSequence,
                Status = t.Status.ToString(),
                ValidationToken = t.ValidationToken,
                QrPayload = $"eventpulse-ticket:{t.ValidationToken}",
                CreatedAt = t.CreatedAt
            })
            .ToList();

        return Ok(new CustomerBookingTicketsResponseDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            EventId = booking.EventId,
            BookingStatus = booking.Status.ToString(),
            TotalAmount = booking.TotalAmount,
            CreatedAt = booking.CreatedAt,
            ConfirmedAt = booking.ConfirmedAt,
            Tickets = ticketDtos
        });
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
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(3),
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
            ExpiresAt = booking.ExpiresAt,
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

    /// <summary>
    /// Cancels a booking for the authenticated customer (US-28 / EP-308).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingCancellationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BookingCancellationResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BookingCancellationResultDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelBooking(
        [FromRoute] Guid id,
        [FromBody] CancelBookingRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized();
        }

        var result = await _cancellationService.CancelBookingAsync(id, customerId, request, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Booking not found or access denied")
            {
                return NotFound(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }
}
