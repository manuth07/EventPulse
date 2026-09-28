using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class PaymentSucceededEventHandlerTests
{
    private readonly BookingDbContext _dbContext;
    private readonly Mock<IBookingConfirmationService> _confirmationServiceMock;
    private readonly Mock<ITicketGenerationService> _ticketGenerationServiceMock;
    private readonly Mock<ILogger<PaymentSucceededEventHandler>> _loggerMock;
    private readonly PaymentSucceededEventHandler _handler;

    public PaymentSucceededEventHandlerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BookingDbContext(dbOptions);

        _confirmationServiceMock = new Mock<IBookingConfirmationService>();
        _ticketGenerationServiceMock = new Mock<ITicketGenerationService>();
        _loggerMock = new Mock<ILogger<PaymentSucceededEventHandler>>();
        _handler = new PaymentSucceededEventHandler(
            _dbContext,
            _confirmationServiceMock.Object,
            _ticketGenerationServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingPendingPayment_ConfirmsBookingAndGeneratesTickets()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = new Booking { Id = bookingId, Status = BookingStatus.Confirmed };
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.Confirmed(booking));

        _ticketGenerationServiceMock
            .Setup(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketGenerationResult.Success(bookingId, new List<Ticket> { new() { Id = Guid.NewGuid(), BookingId = bookingId } }, 1));

        // Act
        await _handler.HandleAsync(paymentEvent);

        // Assert
        _confirmationServiceMock.Verify(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()), Times.Once);
        _ticketGenerationServiceMock.Verify(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingAlreadyConfirmed_StillInvokesTicketGeneration_ForCrashRecovery()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = new Booking { Id = bookingId, Status = BookingStatus.Confirmed };
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.AlreadyConfirmed(booking));

        _ticketGenerationServiceMock
            .Setup(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketGenerationResult.Success(bookingId, new List<Ticket> { new() { Id = Guid.NewGuid(), BookingId = bookingId } }, 0));

        // Act
        await _handler.HandleAsync(paymentEvent);

        // Assert: Ticket generation must STILL be invoked
        _confirmationServiceMock.Verify(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()), Times.Once);
        _ticketGenerationServiceMock.Verify(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConfirmationReturnsNotFound_ThrowsInvalidOperationException_AndDoesNotGenerateTickets()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.NotFound(bookingId));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(paymentEvent));
        Assert.Contains(bookingId.ToString(), ex.Message);
        Assert.Contains("NotFound", ex.Message);

        _ticketGenerationServiceMock.Verify(t => t.GenerateTicketsForBookingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenConfirmationReturnsInvalidState_ThrowsInvalidOperationException_AndDoesNotGenerateTickets()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.InvalidState(bookingId, BookingStatus.Cancelled, "Booking was cancelled."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(paymentEvent));
        Assert.Contains("InvalidState", ex.Message);

        _ticketGenerationServiceMock.Verify(t => t.GenerateTicketsForBookingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketGenerationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = new Booking { Id = bookingId, Status = BookingStatus.Confirmed };
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.Confirmed(booking));

        _ticketGenerationServiceMock
            .Setup(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketGenerationResult.InvalidStatus(bookingId, BookingStatus.PendingPayment));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(paymentEvent));
        Assert.Contains("InvalidStatus", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketGenerationThrows_ExceptionPropagates()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var booking = new Booking { Id = bookingId, Status = BookingStatus.Confirmed };
        var paymentEvent = new PaymentSucceededEvent
        {
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid()
        };

        _confirmationServiceMock
            .Setup(c => c.ConfirmBookingAfterPaymentAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingConfirmationResult.Confirmed(booking));

        _ticketGenerationServiceMock
            .Setup(t => t.GenerateTicketsForBookingAsync(bookingId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Database connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => _handler.HandleAsync(paymentEvent));
    }

    [Fact]
    public async Task HandleAsync_RealServicesIntegration_WhenProcessedTwice_IsIdempotentAndDoesNotDuplicateTickets()
    {
        // Arrange: In-Memory DB
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new BookingDbContext(options);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var tierVipId = Guid.NewGuid();
        var tierRegularId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-INTEG1",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.PendingPayment,
            TotalAmount = 25000.00m,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Items = new List<BookingItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = tierVipId,
                    TicketName = "VIP",
                    Quantity = 2,
                    UnitPrice = 10000.00m,
                    Subtotal = 20000.00m
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = tierRegularId,
                    TicketName = "Regular",
                    Quantity = 1,
                    UnitPrice = 5000.00m,
                    Subtotal = 5000.00m
                }
            }
        };

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        // Real domain services
        var confirmLogger = new Mock<ILogger<BookingConfirmationService>>();
        var confirmationService = new BookingConfirmationService(dbContext, confirmLogger.Object);

        var ticketCodeGenerator = new TicketCodeGenerator(dbContext);
        var validationTokenGenerator = new ValidationTokenGenerator(dbContext);
        var ticketGenLogger = new Mock<ILogger<TicketGenerationService>>();
        var ticketGenerationService = new TicketGenerationService(
            dbContext,
            ticketCodeGenerator,
            validationTokenGenerator,
            ticketGenLogger.Object);

        var handlerLogger = new Mock<ILogger<PaymentSucceededEventHandler>>();
        var realHandler = new PaymentSucceededEventHandler(
            dbContext,
            confirmationService,
            ticketGenerationService,
            handlerLogger.Object);

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            BookingReference = "EP-2026-INTEG1",
            CustomerId = customerId,
            Amount = 25000.00m,
            Currency = "lkr"
        };

        // Act: Pass 1 (first delivery)
        await realHandler.HandleAsync(paymentEvent);

        // Assert: Pass 1
        var confirmedBooking = await dbContext.Bookings.FindAsync(bookingId);
        Assert.NotNull(confirmedBooking);
        Assert.Equal(BookingStatus.Confirmed, confirmedBooking.Status);
        Assert.NotNull(confirmedBooking.ConfirmedAt);

        var ticketsFirstPass = await dbContext.Tickets.Where(t => t.BookingId == bookingId).ToListAsync();
        Assert.Equal(3, ticketsFirstPass.Count); // 2 VIP + 1 Regular = 3
        Assert.Equal(3, ticketsFirstPass.Select(t => t.TicketCode).Distinct().Count());
        Assert.Equal(3, ticketsFirstPass.Select(t => t.ValidationToken).Distinct().Count());
        Assert.All(ticketsFirstPass, t => Assert.Equal(TicketStatus.Valid, t.Status));

        // Act: Pass 2 (duplicate/redelivery event)
        await realHandler.HandleAsync(paymentEvent);

        // Assert: Pass 2 - Still exactly 3 tickets, no duplicates!
        var ticketsSecondPass = await dbContext.Tickets.Where(t => t.BookingId == bookingId).ToListAsync();
        Assert.Equal(3, ticketsSecondPass.Count);

        // Assert: ProcessedIntegrationEvent was stored
        var processedEvent = await dbContext.ProcessedIntegrationEvents.FindAsync(paymentEvent.EventId);
        Assert.NotNull(processedEvent);
        Assert.Equal(paymentEvent.EventId, processedEvent.EventId);
        Assert.Equal(nameof(PaymentSucceededEvent), processedEvent.EventType);
        Assert.Equal(KafkaTopics.PaymentSucceeded, processedEvent.Topic);
    }

    [Fact]
    public async Task HandleAsync_WhenEventAlreadyInInbox_ImmediatelyReturnsWithoutCallingServices()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            EventId = eventId,
            EventType = nameof(PaymentSucceededEvent),
            Topic = KafkaTopics.PaymentSucceeded,
            ProcessedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await _dbContext.SaveChangesAsync();

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            BookingReference = "EP-2026-INBOX",
            CustomerId = Guid.NewGuid(),
            Amount = 1000m,
            Currency = "lkr"
        };

        // Act
        await _handler.HandleAsync(paymentEvent);

        // Assert: Neither confirmation nor ticket generation was invoked!
        _confirmationServiceMock.Verify(
            c => c.ConfirmBookingAfterPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _ticketGenerationServiceMock.Verify(
            t => t.GenerateTicketsForBookingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
