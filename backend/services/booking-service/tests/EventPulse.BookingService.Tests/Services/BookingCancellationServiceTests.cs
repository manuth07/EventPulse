using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class BookingCancellationServiceTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    [Fact]
    public async Task CancelBooking_WhenEligible_SetsStatusToCancelledAndInvalidatesTickets()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-CANCEL-001",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    TicketTypeId = ticketTypeId,
                    TicketName = "General Admission",
                    Quantity = 2,
                    UnitPrice = 50.0m,
                    Subtotal = 100.0m
                }
            },
            Tickets = new List<Ticket>
            {
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid },
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        var request = new CancelBookingRequest { Reason = "Unable to attend" };

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Confirmed", result.PreviousStatus);
        Assert.Equal("Cancelled", result.NewStatus);
        Assert.Equal("EP-2026-CANCEL-001", result.BookingReference);
        Assert.Equal("Booking cancelled successfully", result.Message);

        var updated = await context.Bookings.Include(b => b.Tickets).FirstAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
        Assert.Equal(2, updated.Tickets.Count);
        Assert.All(updated.Tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(
            It.Is<BookingCancelledEvent>(e =>
                e.BookingId == bookingId &&
                e.BookingReference == "EP-2026-CANCEL-001" &&
                e.CustomerId == customerId &&
                e.ReleasedTickets.Count == 1 &&
                e.ReleasedTickets[0].TicketTypeId == ticketTypeId &&
                e.ReleasedTickets[0].Quantity == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelBooking_WhenUserNotOwner_ReturnsIneligibleResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var anotherCustomerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-OWNER-001",
            CustomerId = ownerId,
            Status = BookingStatus.Confirmed
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, anotherCustomerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Booking not found or access denied", result.Message);

        var unchanged = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(unchanged);
        Assert.Equal(BookingStatus.Confirmed, unchanged.Status);

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelBooking_WhenAlreadyCancelled_ReturnsIneligibleResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-ALREADY-CANCELLED",
            CustomerId = customerId,
            Status = BookingStatus.Cancelled
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Booking is already cancelled", result.Message);
        Assert.Equal("Cancelled", result.PreviousStatus);

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelSingleTicket_WhenOtherTicketsExist_SetsTicketCancelledAndMaintainsConfirmedBooking()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticket1Id = Guid.NewGuid();
        var ticket2Id = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-MULTI-TICKET",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = ticket1Id, TicketCode = "TCK-1", TicketTypeId = ticketTypeId, Status = TicketStatus.Valid },
                new Ticket { Id = ticket2Id, TicketCode = "TCK-2", TicketTypeId = ticketTypeId, Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act - Cancel only ticket1
        var result = await service.CancelSingleTicketAsync(ticket1Id, customerId, "Guest cannot come", CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TicketStatus.Cancelled.ToString(), result.Status);
        Assert.Equal(BookingStatus.Confirmed.ToString(), result.ParentBookingStatus);

        var updatedTicket1 = await context.Tickets.FindAsync(ticket1Id);
        var updatedTicket2 = await context.Tickets.FindAsync(ticket2Id);
        var updatedBooking = await context.Bookings.FindAsync(bookingId);

        Assert.NotNull(updatedTicket1);
        Assert.Equal(TicketStatus.Cancelled, updatedTicket1.Status);

        Assert.NotNull(updatedTicket2);
        Assert.Equal(TicketStatus.Valid, updatedTicket2.Status);

        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Confirmed, updatedBooking.Status);

        eventPublisherMock.Verify(p => p.PublishTicketCancelledAsync(
            It.Is<TicketCancelledEvent>(e =>
                e.TicketId == ticket1Id &&
                e.BookingId == bookingId &&
                e.CustomerId == customerId &&
                e.TicketTypeId == ticketTypeId &&
                e.TicketCode == "TCK-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelSingleTicket_WhenLastValidTicket_SetsTicketAndParentBookingCancelled()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticket1Id = Guid.NewGuid();
        var ticket2Id = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-LAST-TICKET",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = ticket1Id, TicketCode = "TCK-1", TicketTypeId = ticketTypeId, Status = TicketStatus.Valid },
                new Ticket { Id = ticket2Id, TicketCode = "TCK-2", TicketTypeId = ticketTypeId, Status = TicketStatus.Cancelled }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act - Cancel the only remaining valid ticket
        var result = await service.CancelSingleTicketAsync(ticket1Id, customerId, "Cancelling last ticket", CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TicketStatus.Cancelled.ToString(), result.Status);
        Assert.Equal(BookingStatus.Cancelled.ToString(), result.ParentBookingStatus);

        var updatedTicket1 = await context.Tickets.FindAsync(ticket1Id);
        var updatedBooking = await context.Bookings.FindAsync(bookingId);

        Assert.NotNull(updatedTicket1);
        Assert.Equal(TicketStatus.Cancelled, updatedTicket1.Status);

        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Cancelled, updatedBooking.Status);

        eventPublisherMock.Verify(p => p.PublishTicketCancelledAsync(
            It.Is<TicketCancelledEvent>(e => e.TicketId == ticket1Id && e.BookingId == bookingId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelSingleTicket_WhenUserNotOwner_ReturnsIneligibleResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var anotherCustomerId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-NOT-OWNER",
            CustomerId = ownerId,
            Status = BookingStatus.Confirmed,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = ticketId, TicketCode = "TCK-1", Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelSingleTicketAsync(ticketId, anotherCustomerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Ticket not found or access denied", result.Message);

        var ticket = await context.Tickets.FindAsync(ticketId);
        Assert.NotNull(ticket);
        Assert.Equal(TicketStatus.Valid, ticket.Status);
    }

    [Fact]
    public async Task CancelSingleTicket_WhenAlreadyCancelled_ReturnsIneligibleResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-ALREADY-CANCELLED",
            CustomerId = customerId,
            Status = BookingStatus.Confirmed,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = ticketId, TicketCode = "TCK-1", Status = TicketStatus.Cancelled }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelSingleTicketAsync(ticketId, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Ticket is already Cancelled", result.Message);
    }
}
