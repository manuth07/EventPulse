using EventPulse.BookingService.Data;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class PaymentRefundedEventHandlerTests
{
    private readonly BookingDbContext _dbContext;
    private readonly Mock<ISalesReportingService> _salesReportingServiceMock;
    private readonly PaymentRefundedEventHandler _handler;

    public PaymentRefundedEventHandlerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BookingDbContext(dbOptions);

        _salesReportingServiceMock = new Mock<ISalesReportingService>();
        _handler = new PaymentRefundedEventHandler(
            _dbContext,
            _salesReportingServiceMock.Object,
            NullLogger<PaymentRefundedEventHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_FirstTime_CallsApplyRefundAdjustmentAndRecordsInbox()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var refundedAt = DateTimeOffset.UtcNow;
        var refundedEvent = new PaymentRefundedEvent(
            refundId,
            paymentId,
            bookingId,
            50.00m,
            "Completed",
            refundedAt);

        // Act
        await _handler.HandleAsync(refundedEvent);

        // Assert
        _salesReportingServiceMock.Verify(s => s.ApplyRefundAdjustmentAsync(
            bookingId,
            refundId,
            50.00m,
            "Completed",
            It.IsAny<string>(),
            refundedAt,
            It.IsAny<CancellationToken>()), Times.Once);

        var recorded = await _dbContext.ProcessedIntegrationEvents.AnyAsync(e => e.EventId == refundId);
        Assert.True(recorded);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_SkipsProcessing()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var refundedEvent = new PaymentRefundedEvent(
            refundId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            50.00m,
            "Completed",
            DateTimeOffset.UtcNow);

        _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            EventId = refundId,
            EventType = nameof(PaymentRefundedEvent),
            Topic = "payment-refunded",
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _handler.HandleAsync(refundedEvent);

        // Assert
        _salesReportingServiceMock.Verify(s => s.ApplyRefundAdjustmentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
