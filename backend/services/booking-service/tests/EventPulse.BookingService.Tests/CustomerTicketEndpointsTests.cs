using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class CustomerTicketEndpointsTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    private BookingsController CreateBookingsController(BookingDbContext context, Guid? customerId = null, bool isAdmin = false)
    {
        var loggerMock = new Mock<ILogger<BookingsController>>();
        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var referenceGeneratorMock = new Mock<IBookingReferenceGenerator>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var cartServiceMock = new Mock<ICartService>();
        var bookingHistoryServiceMock = new Mock<IBookingHistoryService>();

        var controller = new BookingsController(
            context,
            eventClientMock.Object,
            referenceGeneratorMock.Object,
            eventPublisherMock.Object,
            loggerMock.Object,
            cartServiceMock.Object,
            bookingHistoryServiceMock.Object);

        var claims = new List<Claim>();
        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, customerId.Value.ToString()));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        }

        var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "mock") : new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    private TicketsController CreateTicketsController(BookingDbContext context, Guid? customerId = null, bool isAdmin = false)
    {
        var controller = new TicketsController(context);

        var claims = new List<Claim>();
        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, customerId.Value.ToString()));
        }
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        }

        var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "mock") : new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public async Task GetBookingTickets_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var controller = CreateBookingsController(context, customerId: null);

        // Act
        var result = await controller.GetBookingTickets(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetBookingTickets_WhenBookingNotFound_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var controller = CreateBookingsController(context, customerId);

        // Act
        var result = await controller.GetBookingTickets(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    [Fact]
    public async Task GetBookingTickets_WhenNotOwnerAndNotAdmin_ReturnsForbidden()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-TEST01",
            CustomerId = ownerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();

        var controller = CreateBookingsController(context, otherCustomerId);

        // Act
        var result = await controller.GetBookingTickets(booking.Id, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetBookingTickets_WhenPendingBooking_ReturnsLogicalEmptyTicketsResponse()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-PEND01",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = 50m
        };
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();

        var controller = CreateBookingsController(context, customerId);

        // Act
        var result = await controller.GetBookingTickets(booking.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CustomerBookingTicketsResponseDto>(okResult.Value);

        Assert.Equal(booking.Id, response.BookingId);
        Assert.Equal("EP-2026-PEND01", response.BookingReference);
        Assert.Equal("PendingPayment", response.BookingStatus);
        Assert.Empty(response.Tickets);
    }

    [Fact]
    public async Task GetBookingTickets_WhenConfirmedWithMultipleTickets_ReturnsOrderedTicketsWithOpaqueQrPayload()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-CONF01",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 150m,
            ConfirmedAt = DateTimeOffset.UtcNow
        };

        var ticket1 = new Ticket
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingItemId = itemId,
            TicketTypeId = Guid.NewGuid(),
            TicketName = "VIP Admission",
            TicketSequence = 1,
            EventId = eventId,
            TicketCode = "EP-TKT-2026-AAA11111",
            ValidationToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Status = TicketStatus.Valid,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var ticket2 = new Ticket
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingItemId = itemId,
            TicketTypeId = ticket1.TicketTypeId,
            TicketName = "VIP Admission",
            TicketSequence = 2,
            EventId = eventId,
            TicketCode = "EP-TKT-2026-BBB22222",
            ValidationToken = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            Status = TicketStatus.Valid,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Bookings.AddAsync(booking);
        await context.Tickets.AddRangeAsync(ticket2, ticket1); // Inserted in reverse to verify ordering
        await context.SaveChangesAsync();

        var controller = CreateBookingsController(context, customerId);

        // Act
        var result = await controller.GetBookingTickets(bookingId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CustomerBookingTicketsResponseDto>(okResult.Value);

        Assert.Equal(bookingId, response.BookingId);
        Assert.Equal("EP-2026-CONF01", response.BookingReference);
        Assert.Equal("Confirmed", response.BookingStatus);
        Assert.Equal(2, response.Tickets.Count);

        // Check ordering by sequence
        Assert.Equal(1, response.Tickets[0].TicketSequence);
        Assert.Equal("EP-TKT-2026-AAA11111", response.Tickets[0].TicketCode);
        Assert.Equal("Valid", response.Tickets[0].Status);
        Assert.Equal($"eventpulse-ticket:{ticket1.ValidationToken}", response.Tickets[0].QrPayload);

        Assert.Equal(2, response.Tickets[1].TicketSequence);
        Assert.Equal("EP-TKT-2026-BBB22222", response.Tickets[1].TicketCode);
        Assert.Equal("Valid", response.Tickets[1].Status);
        Assert.Equal($"eventpulse-ticket:{ticket2.ValidationToken}", response.Tickets[1].QrPayload);
    }

    [Fact]
    public async Task GetBookingTickets_WhenAdminUser_CanAccessAnyBooking()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-ADMIN01",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 50m
        };
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();

        var controller = CreateBookingsController(context, adminId, isAdmin: true);

        // Act
        var result = await controller.GetBookingTickets(booking.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CustomerBookingTicketsResponseDto>(okResult.Value);
        Assert.Equal(booking.Id, response.BookingId);
    }

    [Fact]
    public async Task GetTicketById_WhenOwner_ReturnsTicketWithOpaqueQrPayload()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-SING01",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            BookingItemId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            TicketName = "General Admission",
            TicketSequence = 1,
            EventId = booking.EventId,
            TicketCode = "EP-TKT-2026-SINGLE1",
            ValidationToken = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
            Status = TicketStatus.Valid,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Bookings.AddAsync(booking);
        await context.Tickets.AddAsync(ticket);
        await context.SaveChangesAsync();

        var controller = CreateTicketsController(context, customerId);

        // Act
        var result = await controller.GetTicketById(ticket.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CustomerTicketDto>(okResult.Value);

        Assert.Equal(ticket.Id, dto.TicketId);
        Assert.Equal("EP-TKT-2026-SINGLE1", dto.TicketCode);
        Assert.Equal("EP-2026-SING01", dto.BookingReference);
        Assert.Equal($"eventpulse-ticket:{ticket.ValidationToken}", dto.QrPayload);
    }

    [Fact]
    public async Task GetTicketById_WhenOtherCustomer_ReturnsForbidden()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-SECR01",
            CustomerId = ownerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            BookingItemId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            TicketName = "General Admission",
            TicketSequence = 1,
            EventId = booking.EventId,
            TicketCode = "EP-TKT-2026-SECRET",
            ValidationToken = "1111222233334444555566667777888811112222333344445555666677778888",
            Status = TicketStatus.Valid,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Bookings.AddAsync(booking);
        await context.Tickets.AddAsync(ticket);
        await context.SaveChangesAsync();

        var controller = CreateTicketsController(context, intruderId);

        // Act
        var result = await controller.GetTicketById(ticket.Id, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var controller = CreateTicketsController(context, customerId);

        // Act
        var result = await controller.GetTicketById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
