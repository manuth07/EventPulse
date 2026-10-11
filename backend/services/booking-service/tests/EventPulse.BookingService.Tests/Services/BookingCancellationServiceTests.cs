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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        Assert.NotNull(updated.CancelledAt);
        Assert.Equal(customerId.ToString(), updated.CancelledBy);
        Assert.Equal("Unable to attend", updated.CancellationReason);
        Assert.Equal(customerId.ToString(), result.CancelledBy);
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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

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
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelSingleTicketAsync(ticketId, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Ticket is already Cancelled", result.Message);
    }

    [Fact]
    public async Task CancelBooking_WhenConfirmedWithAmount_EmitsBookingRefundRequestedEvent()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-REFUND-FULL",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 250.0m,
            Items = new List<BookingItem>(),
            Tickets = new List<Ticket>()
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, new CancelBookingRequest { Reason = "Emergency" }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(
            It.Is<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(e =>
                e.BookingId == bookingId &&
                e.RefundAmount == 250.0m &&
                e.Reason == "Emergency"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelSingleTicket_WhenConfirmed_EmitsBookingRefundRequestedEvent()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-REFUND-PARTIAL",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 150.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    TicketTypeId = ticketTypeId,
                    TicketName = "VIP Pass",
                    Quantity = 2,
                    UnitPrice = 75.0m,
                    Subtotal = 150.0m
                }
            },
            Tickets = new List<Ticket>
            {
                new Ticket { Id = ticketId, TicketCode = "TCK-VIP-1", TicketTypeId = ticketTypeId, Status = TicketStatus.Valid },
                new Ticket { Id = Guid.NewGuid(), TicketCode = "TCK-VIP-2", TicketTypeId = ticketTypeId, Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(context, eventPublisherMock.Object, new Mock<IEventAvailabilityClient>().Object, NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelSingleTicketAsync(ticketId, customerId, "VIP cancel", CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(
            It.Is<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(e =>
                e.BookingId == bookingId &&
                e.RefundAmount == 75.0m &&
                e.Reason == "VIP cancel"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public TestTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    private class FailingBookingDbContext : BookingDbContext
    {
        public FailingBookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Simulated database failure during cancellation save");
        }
    }

    private class ConcurrencySimulatingBookingDbContext : BookingDbContext
    {
        private readonly DbContextOptions<BookingDbContext> _options;
        private readonly Guid _targetBookingId;
        private bool _hasSimulated = false;

        public ConcurrencySimulatingBookingDbContext(DbContextOptions<BookingDbContext> options, Guid targetBookingId)
            : base(options)
        {
            _options = options;
            _targetBookingId = targetBookingId;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_hasSimulated)
            {
                _hasSimulated = true;
                using (var concurrentContext = new BookingDbContext(_options))
                {
                    var concurrentBooking = await concurrentContext.Bookings.FindAsync(_targetBookingId);
                    if (concurrentBooking != null)
                    {
                        concurrentBooking.Status = BookingStatus.Cancelled;
                        concurrentBooking.CancelledAt = DateTimeOffset.UtcNow;
                        concurrentBooking.CancelledBy = "concurrent-actor";
                        await concurrentContext.SaveChangesAsync(cancellationToken);
                    }
                }

                throw new DbUpdateConcurrencyException(
                    "Simulated concurrent cancellation conflict",
                    new List<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task CancelBooking_WithTimeProvider_RecordsExactTimestampAndActor()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 10, 11, 14, 30, 0, TimeSpan.Zero);
        var testClock = new TestTimeProvider(fixedTime);

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TIME-TEST",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 50m
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            new Mock<IEventAvailabilityClient>().Object,
            NullLogger<BookingCancellationService>.Instance,
            timeProvider: testClock);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, new CancelBookingRequest { Reason = "Customer reschedule" }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(fixedTime, result.CancelledAt);
        Assert.Equal(customerId.ToString(), result.CancelledBy);

        var updated = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(updated);
        Assert.Equal(fixedTime, updated.CancelledAt);
        Assert.Equal(customerId.ToString(), updated.CancelledBy);
        Assert.Equal("Customer reschedule", updated.CancellationReason);
    }

    [Fact]
    public async Task CancelBooking_WhenIneligibleDueToCutoff_ReturnsIneligibleResultAndDoesNotCancel()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventDate = new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
        var currentTime = eventDate.AddHours(-24); // 24h before event (< 48h cutoff)
        var testClock = new TestTimeProvider(currentTime);

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-CUTOFF-TEST",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventClientMock = new Mock<IEventAvailabilityClient>();
        eventClientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, EventDate = eventDate.UtcDateTime, Title = "Soon Event" });

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            eventClientMock.Object,
            NullLogger<BookingCancellationService>.Instance,
            timeProvider: testClock);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(CancellationReasonCodes.CancellationWindowClosed, result.ReasonCode);
        Assert.Equal("The cancellation window for this event has closed", result.Message);

        var unchanged = await context.Bookings.Include(b => b.Tickets).FirstAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Confirmed, unchanged.Status);
        Assert.All(unchanged.Tickets, t => Assert.Equal(TicketStatus.Valid, t.Status));
        Assert.Null(unchanged.CancelledAt);

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(It.IsAny<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelBooking_WhenIneligibleDueToEventStarted_ReturnsIneligibleResultAndDoesNotCancel()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventDate = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var currentTime = eventDate.AddHours(1); // 1h after start
        var testClock = new TestTimeProvider(currentTime);

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-STARTED-TEST",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 80m
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventClientMock = new Mock<IEventAvailabilityClient>();
        eventClientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, EventDate = eventDate.UtcDateTime, Title = "Started Event" });

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            eventClientMock.Object,
            NullLogger<BookingCancellationService>.Instance,
            timeProvider: testClock);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(CancellationReasonCodes.EventAlreadyStarted, result.ReasonCode);
        Assert.Equal("Cannot cancel tickets for an event that has already started or concluded", result.Message);

        var unchanged = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(unchanged);
        Assert.Equal(BookingStatus.Confirmed, unchanged.Status);
    }

    [Fact]
    public async Task CancelBooking_DuplicateSequentialRequests_SecondReturnsAlreadyCancelled()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-SEQ-DUP",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            new Mock<IEventAvailabilityClient>().Object,
            NullLogger<BookingCancellationService>.Instance);

        // Act - Call 1 (Success)
        var result1 = await service.CancelBookingAsync(bookingId, customerId, new CancelBookingRequest { Reason = "Attempt 1" }, CancellationToken.None);

        // Act - Call 2 (Duplicate)
        var result2 = await service.CancelBookingAsync(bookingId, customerId, new CancelBookingRequest { Reason = "Attempt 2" }, CancellationToken.None);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Equal(CancellationReasonCodes.AlreadyCancelled, result2.ReasonCode);
        Assert.Equal("Booking is already cancelled", result2.Message);

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(It.IsAny<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelBooking_WhenConcurrentCancellationOccurs_HandlesDbUpdateConcurrencyGracefully()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        using (var setupContext = new BookingDbContext(options))
        {
            setupContext.Bookings.Add(new Booking
            {
                Id = bookingId,
                BookingReference = "EP-2026-CONCURRENCY",
                CustomerId = customerId,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                TotalAmount = 120m
            });
            await setupContext.SaveChangesAsync();
        }

        using var concurrencyContext = new ConcurrencySimulatingBookingDbContext(options, bookingId);
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var eventClientMock = new Mock<IEventAvailabilityClient>();

        var service = new BookingCancellationService(
            concurrencyContext,
            eventPublisherMock.Object,
            eventClientMock.Object,
            NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, new CancelBookingRequest { Reason = "Race condition" }, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(CancellationReasonCodes.AlreadyCancelled, result.ReasonCode);
        Assert.Equal("Booking is already cancelled", result.Message);

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(It.IsAny<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelBooking_WhenDatabaseFails_RethrowsAndEmitsNoEvents()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        using (var setupContext = new BookingDbContext(options))
        {
            setupContext.Bookings.Add(new Booking
            {
                Id = bookingId,
                BookingReference = "EP-2026-DB-FAIL",
                CustomerId = customerId,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                TotalAmount = 150m
            });
            await setupContext.SaveChangesAsync();
        }

        using var failingContext = new FailingBookingDbContext(options);
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var eventClientMock = new Mock<IEventAvailabilityClient>();

        var service = new BookingCancellationService(
            failingContext,
            eventPublisherMock.Object,
            eventClientMock.Object,
            NullLogger<BookingCancellationService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None));

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(It.IsAny<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        eventClientMock.Verify(c => c.RestoreInventoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<ReserveInventoryItemDto>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelBooking_InvalidatesAllTicketsAcrossMultipleTicketTypes()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var type1Id = Guid.NewGuid();
        var type2Id = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-MULTI-TYPES",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 250m,
            Items = new List<BookingItem>
            {
                new BookingItem { Id = Guid.NewGuid(), TicketTypeId = type1Id, TicketName = "VIP", Quantity = 2, UnitPrice = 100m, Subtotal = 200m },
                new BookingItem { Id = Guid.NewGuid(), TicketTypeId = type2Id, TicketName = "Standard", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }
            },
            Tickets = new List<Ticket>
            {
                new Ticket { Id = Guid.NewGuid(), TicketTypeId = type1Id, TicketCode = "T-VIP-1", Status = TicketStatus.Valid },
                new Ticket { Id = Guid.NewGuid(), TicketTypeId = type1Id, TicketCode = "T-VIP-2", Status = TicketStatus.Valid },
                new Ticket { Id = Guid.NewGuid(), TicketTypeId = type2Id, TicketCode = "T-STD-1", Status = TicketStatus.Valid }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            new Mock<IEventAvailabilityClient>().Object,
            NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var updated = await context.Bookings.Include(b => b.Tickets).FirstAsync(b => b.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
        Assert.Equal(3, updated.Tickets.Count);
        Assert.All(updated.Tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));

        eventPublisherMock.Verify(p => p.PublishBookingCancelledAsync(
            It.Is<BookingCancelledEvent>(e =>
                e.BookingId == bookingId &&
                e.ReleasedTickets.Count == 2 &&
                e.ReleasedTickets.Any(rt => rt.TicketTypeId == type1Id && rt.Quantity == 2) &&
                e.ReleasedTickets.Any(rt => rt.TicketTypeId == type2Id && rt.Quantity == 1)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelBooking_WhenPendingPayment_ReleasesInventoryAndDoesNotEmitRefund()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-PENDING-CANCEL",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.PendingPayment,
            TotalAmount = 100m,
            Items = new List<BookingItem>(),
            Tickets = new List<Ticket>()
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();

        var service = new BookingCancellationService(
            context,
            eventPublisherMock.Object,
            eventClientMock.Object,
            NullLogger<BookingCancellationService>.Instance);

        // Act
        var result = await service.CancelBookingAsync(bookingId, customerId, null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("PendingPayment", result.PreviousStatus);
        Assert.Equal("Cancelled", result.NewStatus);

        eventClientMock.Verify(c => c.ReleaseInventoryAsync(eventId, bookingId, It.IsAny<CancellationToken>()), Times.Once);
        eventClientMock.Verify(c => c.RestoreInventoryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<ReserveInventoryItemDto>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        eventPublisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(It.IsAny<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
