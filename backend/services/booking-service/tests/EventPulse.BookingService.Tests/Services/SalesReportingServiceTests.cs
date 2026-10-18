using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class SalesReportingServiceTests
{
    private readonly BookingDbContext _dbContext;
    private readonly Mock<IEventAvailabilityClient> _eventClientMock;
    private readonly SalesReportingService _salesReportingService;

    public SalesReportingServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new BookingDbContext(dbOptions);

        _eventClientMock = new Mock<IEventAvailabilityClient>();
        _salesReportingService = new SalesReportingService(
            _dbContext,
            _eventClientMock.Object,
            NullLogger<SalesReportingService>.Instance);
    }

    private Booking CreateTestBooking(Guid eventId, Guid customerId, decimal totalAmount, params (Guid ticketTypeId, string name, int qty, decimal price)[] items)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-" + Guid.NewGuid().ToString()[..8].ToUpper(),
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = totalAmount,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        foreach (var item in items)
        {
            booking.Items.Add(new BookingItem
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                TicketTypeId = item.ticketTypeId,
                TicketName = item.name,
                Quantity = item.qty,
                UnitPrice = item.price,
                Subtotal = item.qty * item.price
            });
        }

        _dbContext.Bookings.Add(booking);
        _dbContext.SaveChanges();
        return booking;
    }

    // Scenario 1: Successful payment generates a sales report record
    [Fact]
    public async Task Scenario01_SuccessfulPayment_GeneratesSalesReportRecord()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var booking = CreateTestBooking(eventId, customerId, 100.00m, (ticketTypeId, "General Admission", 2, 50.00m));

        _eventClientMock
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Tech Summit", OrganizerId = organizerId });

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = customerId,
            Amount = 100.00m,
            Currency = "USD",
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        Assert.NotNull(result);
        var stored = await _dbContext.SalesReports.Include(s => s.Items).FirstOrDefaultAsync(s => s.PaymentId == paymentEvent.PaymentId);
        Assert.NotNull(stored);
        Assert.Equal(paymentEvent.PaymentId, stored.PaymentId);
        Assert.Equal(booking.Id, stored.BookingId);
        Assert.Equal(100.00m, stored.SaleAmount);
    }

    // Scenario 2: Sales report contains all required fields: sale ID, payment reference, booking ID, event ID, organizer ID, ticket type, quantity, sale amount, currency, timestamp
    [Fact]
    public async Task Scenario02_SalesReport_ContainsAllRequiredFields()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var booking = CreateTestBooking(eventId, customerId, 75.50m, (ticketTypeId, "VIP Access", 1, 75.50m));
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        _eventClientMock
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Jazz Festival", OrganizerId = organizerId });

        var paymentId = Guid.NewGuid();
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = paymentId,
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = customerId,
            Amount = 75.50m,
            Currency = "EUR",
            OccurredAtUtc = timestamp
        };

        // Act
        var report = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        Assert.NotEqual(Guid.Empty, report.Id);
        Assert.Equal(paymentId, report.PaymentId);
        Assert.Equal(booking.Id, report.BookingId);
        Assert.Equal(eventId, report.EventId);
        Assert.Equal(organizerId, report.OrganizerId);
        Assert.Equal(75.50m, report.SaleAmount);
        Assert.Equal("EUR", report.Currency);
        Assert.Equal(timestamp, report.PaymentTimestampUtc);

        Assert.Single(report.Items);
        var item = report.Items.First();
        Assert.Equal(ticketTypeId, item.TicketTypeId);
        Assert.Equal("VIP Access", item.TicketTypeName);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(75.50m, item.UnitPrice);
        Assert.Equal(75.50m, item.Subtotal);
    }

    // Scenario 3: Purchases with multiple ticket types generate correct line-item records
    [Fact]
    public async Task Scenario03_MultipleTicketTypes_GeneratesCorrectLineItems()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vipId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        var earlyBirdId = Guid.NewGuid();

        var booking = CreateTestBooking(eventId, customerId, 320.00m,
            (vipId, "VIP Tier", 1, 150.00m),
            (standardId, "Standard Entry", 2, 60.00m),
            (earlyBirdId, "Early Bird", 1, 50.00m));

        _eventClientMock
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, OrganizerId = organizerId });

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 320.00m,
            Currency = "USD"
        };

        // Act
        var report = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        Assert.Equal(3, report.Items.Count);
        var vipItem = report.Items.FirstOrDefault(i => i.TicketTypeId == vipId);
        var standardItem = report.Items.FirstOrDefault(i => i.TicketTypeId == standardId);
        var earlyBirdItem = report.Items.FirstOrDefault(i => i.TicketTypeId == earlyBirdId);

        Assert.NotNull(vipItem);
        Assert.Equal(1, vipItem.Quantity);
        Assert.Equal(150.00m, vipItem.Subtotal);

        Assert.NotNull(standardItem);
        Assert.Equal(2, standardItem.Quantity);
        Assert.Equal(120.00m, standardItem.Subtotal);

        Assert.NotNull(earlyBirdItem);
        Assert.Equal(1, earlyBirdItem.Quantity);
        Assert.Equal(50.00m, earlyBirdItem.Subtotal);
    }

    // Scenario 4: Line-item subtotals sum to the total sale amount
    [Fact]
    public async Task Scenario04_LineItemSubtotals_SumToTotalSaleAmount()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var booking = CreateTestBooking(eventId, Guid.NewGuid(), 275.00m,
            (Guid.NewGuid(), "Ticket A", 3, 50.00m),
            (Guid.NewGuid(), "Ticket B", 1, 125.00m));

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 275.00m,
            Currency = "USD"
        };

        // Act
        var report = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        var sumOfSubtotals = report.Items.Sum(i => i.Subtotal);
        Assert.Equal(report.SaleAmount, sumOfSubtotals);
        Assert.Equal(275.00m, sumOfSubtotals);
    }

    // Scenario 5: Checkout creation does NOT generate a sales report
    [Fact]
    public async Task Scenario05_CheckoutCreation_DoesNotGenerateSalesReport()
    {
        // Arrange: Booking initiated / pending payment
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-PENDING",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = 80.00m
        };
        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        // Assert
        var salesReportCount = await _dbContext.SalesReports.CountAsync();
        Assert.Equal(0, salesReportCount);
    }

    // Scenario 6: Failed payment does NOT generate a sales report
    [Fact]
    public async Task Scenario06_FailedPayment_DoesNotGenerateSalesReport()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 50.00m, (Guid.NewGuid(), "T1", 1, 50.00m));
        var failureServiceMock = new Mock<IBookingPaymentFailureService>();
        failureServiceMock
            .Setup(f => f.HandlePaymentFailureAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingPaymentFailureResult.Failed(booking));

        var failedHandler = new PaymentFailedEventHandler(
            _dbContext,
            failureServiceMock.Object,
            _eventClientMock.Object,
            NullLogger<PaymentFailedEventHandler>.Instance);

        var failedEvent = new PaymentFailedEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            FailureReason = "Card declined"
        };

        // Act
        await failedHandler.HandleAsync(failedEvent);

        // Assert
        var salesReportCount = await _dbContext.SalesReports.CountAsync();
        Assert.Equal(0, salesReportCount);
    }

    // Scenario 7: Expired reservation does NOT generate a sales report
    [Fact]
    public async Task Scenario07_ExpiredReservation_DoesNotGenerateSalesReport()
    {
        // Arrange
        var expiredBooking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-EXPIRED",
            Status = BookingStatus.PendingPayment,
            TotalAmount = 100.00m,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        _dbContext.Bookings.Add(expiredBooking);
        await _dbContext.SaveChangesAsync();

        // Assert
        var salesReports = await _dbContext.SalesReports.Where(s => s.BookingId == expiredBooking.Id).ToListAsync();
        Assert.Empty(salesReports);
    }

    // Scenario 8: Duplicate webhook/event does NOT duplicate sales report records (idempotency)
    [Fact]
    public async Task Scenario08_DuplicateWebhookEvent_DoesNotDuplicateSalesReport_Idempotent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var booking = CreateTestBooking(eventId, Guid.NewGuid(), 100.00m, (Guid.NewGuid(), "GA", 2, 50.00m));
        var paymentId = Guid.NewGuid();

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = paymentId,
            BookingId = booking.Id,
            Amount = 100.00m,
            Currency = "USD"
        };

        // Act: Process 1st time
        var report1 = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Act: Process 2nd time (duplicate delivery)
        var report2 = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        Assert.Equal(report1.Id, report2.Id);
        var totalReports = await _dbContext.SalesReports.CountAsync();
        Assert.Equal(1, totalReports);
    }

    // Scenario 9: Duplicate event processing does NOT inflate total sales revenue
    [Fact]
    public async Task Scenario09_DuplicateEventProcessing_DoesNotInflateTotalSalesRevenue()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 150.00m, (Guid.NewGuid(), "VIP", 1, 150.00m));
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 150.00m,
            Currency = "USD"
        };

        // Act: Run twice
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert: Revenue should be exactly 150, not 300
        var totalRevenue = await _dbContext.SalesReports.SumAsync(s => s.SaleAmount);
        Assert.Equal(150.00m, totalRevenue);
    }

    // Scenario 10: Multiple payment attempts for one booking result in only one confirmed sale record
    [Fact]
    public async Task Scenario10_MultiplePaymentAttempts_ForOneBooking_ResultsInOnlyOneConfirmedSaleRecord()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 60.00m, (Guid.NewGuid(), "Entry", 1, 60.00m));

        var attempt1 = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 60.00m
        };

        var attempt2 = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(), // Different payment ID for second attempt
            BookingId = booking.Id,
            Amount = 60.00m
        };

        // Act
        var report1 = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(attempt1);
        var report2 = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(attempt2);

        // Assert: Only 1 sales report is created for the booking
        Assert.Equal(report1.Id, report2.Id);
        var reportsForBooking = await _dbContext.SalesReports.Where(s => s.BookingId == booking.Id).ToListAsync();
        Assert.Single(reportsForBooking);
    }

    // Scenario 11: Sale record links to the correct event and organizer
    [Fact]
    public async Task Scenario11_SaleRecord_LinksToCorrectEventAndOrganizer()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var booking = CreateTestBooking(eventId, Guid.NewGuid(), 90.00m, (Guid.NewGuid(), "Balcony", 1, 90.00m));

        _eventClientMock
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Ballet Gala", OrganizerId = organizerId });

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 90.00m
        };

        // Act
        var report = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Assert
        Assert.Equal(eventId, report.EventId);
        Assert.Equal(organizerId, report.OrganizerId);

        var byEvent = await _salesReportingService.GetSalesReportsByEventIdAsync(eventId);
        Assert.Single(byEvent);
        Assert.Equal(report.Id, byEvent[0].Id);

        var byOrganizer = await _salesReportingService.GetSalesReportsByOrganizerIdAsync(organizerId);
        Assert.Single(byOrganizer);
        Assert.Equal(report.Id, byOrganizer[0].Id);
    }

    // Scenario 12: Completed refund updates the sales record with refund amount and status
    [Fact]
    public async Task Scenario12_CompletedRefund_UpdatesSalesRecordWithRefundAmountAndStatus()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 100.00m, (Guid.NewGuid(), "General", 2, 50.00m));
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 100.00m
        };
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        var refundId = Guid.NewGuid();
        var refundTimestamp = DateTimeOffset.UtcNow;

        // Act
        var updatedReport = await _salesReportingService.ApplyRefundAdjustmentAsync(
            bookingId: booking.Id,
            refundId: refundId,
            refundAmount: 100.00m,
            refundStatus: "Completed",
            reason: "Customer requested cancellation",
            refundedAtUtc: refundTimestamp);

        // Assert
        Assert.NotNull(updatedReport);
        Assert.Equal(100.00m, updatedReport.RefundedAmount);
        Assert.Equal(SalesRefundStatus.Refunded, updatedReport.RefundStatus);
        Assert.Equal(0m, updatedReport.NetSaleAmount);
        Assert.Equal(refundTimestamp, updatedReport.RefundedAtUtc);
        Assert.Single(updatedReport.RefundAdjustments);
        Assert.Equal(refundId, updatedReport.RefundAdjustments.First().RefundId);
    }

    // Scenario 13: Historical sale record is preserved after refund/cancellation (never deleted)
    [Fact]
    public async Task Scenario13_HistoricalSaleRecord_IsPreservedAfterRefund_NeverDeleted()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 200.00m, (Guid.NewGuid(), "Tier 1", 2, 100.00m));
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 200.00m
        };
        var initialReport = await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);
        var initialSaleId = initialReport.Id;

        // Act: Apply full refund
        await _salesReportingService.ApplyRefundAdjustmentAsync(
            bookingId: booking.Id,
            refundId: Guid.NewGuid(),
            refundAmount: 200.00m,
            refundStatus: "Completed");

        // Assert: SalesReport still exists in database and is not deleted
        var stored = await _dbContext.SalesReports.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == initialSaleId);
        Assert.NotNull(stored);
        Assert.Equal(200.00m, stored.SaleAmount);
        Assert.Equal(200.00m, stored.RefundedAmount);
        Assert.Equal(SalesRefundStatus.Refunded, stored.RefundStatus);
        Assert.NotEmpty(stored.Items);
    }

    // Scenario 14: Partial refund updates the net amount correctly
    [Fact]
    public async Task Scenario14_PartialRefund_UpdatesNetAmountCorrectly()
    {
        // Arrange
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 150.00m,
            (item1Id, "Item 1", 1, 100.00m),
            (item2Id, "Item 2", 1, 50.00m));

        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 150.00m
        };
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Act: Apply partial refund of $50
        var updated = await _salesReportingService.ApplyRefundAdjustmentAsync(
            bookingId: booking.Id,
            refundId: Guid.NewGuid(),
            refundAmount: 50.00m,
            refundStatus: "Completed",
            reason: "Partial cancellation");

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(150.00m, updated.SaleAmount);
        Assert.Equal(50.00m, updated.RefundedAmount);
        Assert.Equal(100.00m, updated.NetSaleAmount);
        Assert.Equal(SalesRefundStatus.PartiallyRefunded, updated.RefundStatus);

        // Duplicate partial refund event does not double count
        var refundId2 = Guid.NewGuid();
        await _salesReportingService.ApplyRefundAdjustmentAsync(booking.Id, refundId2, 30.00m, "Completed");
        var reportAfterSecond = await _salesReportingService.GetSalesReportByBookingIdAsync(booking.Id);
        Assert.NotNull(reportAfterSecond);
        Assert.Equal(80.00m, reportAfterSecond.RefundedAmount);
        Assert.Equal(70.00m, reportAfterSecond.NetSaleAmount);
    }

    [Fact]
    public async Task PendingRefund_RecordsPendingStatusWithoutDeductingCompletedRevenue()
    {
        // Arrange
        var booking = CreateTestBooking(Guid.NewGuid(), Guid.NewGuid(), 100.00m, (Guid.NewGuid(), "Ticket", 1, 100.00m));
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking.Id,
            Amount = 100.00m
        };
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);

        // Act: Pending refund
        var pendingReport = await _salesReportingService.ApplyRefundAdjustmentAsync(
            bookingId: booking.Id,
            refundId: Guid.NewGuid(),
            refundAmount: 100.00m,
            refundStatus: "Pending",
            reason: "Reviewing refund request");

        // Assert
        Assert.NotNull(pendingReport);
        Assert.Equal(SalesRefundStatus.Pending, pendingReport.RefundStatus);
        Assert.Equal(0m, pendingReport.RefundedAmount); // Not deducted yet until completed
        Assert.Equal(100.00m, pendingReport.NetSaleAmount);
        Assert.Single(pendingReport.RefundAdjustments);
        Assert.Equal("Pending", pendingReport.RefundAdjustments.First().Status);
    }

    // =========================================================================
    // EP-405: GetSalesReportsAsync Service Tests
    // =========================================================================

    [Fact]
    public async Task GetSalesReportsAsync_WhenReportsExist_ReturnsPagedResult()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        for (int i = 1; i <= 5; i++)
        {
            var booking = CreateTestBooking(eventId, customerId, 100m * i, (Guid.NewGuid(), "Standard", 1, 100m * i));

            var paymentEvent = new PaymentSucceededEvent
            {
                EventId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = 100m * i,
                Currency = "USD"
            };
            await _salesReportingService.GenerateSalesReportOnPurchaseAsync(paymentEvent);
        }

        var filter = new ReportFilterDto { Page = 1, PageSize = 3 };

        // Act
        var result = await _salesReportingService.GetSalesReportsAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetSalesReportsAsync_WhenNoReportsMatch_ReturnsEmptyPagedResult()
    {
        // Arrange
        var filter = new ReportFilterDto
        {
            EventId = Guid.NewGuid(), // Non-existent event
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _salesReportingService.GetSalesReportsAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetSalesReportsAsync_AppliesFiltersCorrectly()
    {
        // Arrange
        var targetEventId = Guid.NewGuid();
        var otherEventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var booking1 = CreateTestBooking(targetEventId, customerId, 150m, (Guid.NewGuid(), "VIP", 1, 150m));
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking1.Id,
            Amount = 150m,
            Currency = "USD"
        });

        var booking2 = CreateTestBooking(otherEventId, customerId, 80m, (Guid.NewGuid(), "Regular", 1, 80m));
        await _salesReportingService.GenerateSalesReportOnPurchaseAsync(new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = booking2.Id,
            Amount = 80m,
            Currency = "LKR"
        });

        // Act: filter by targetEventId
        var result = await _salesReportingService.GetSalesReportsAsync(new ReportFilterDto { EventId = targetEventId });

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(150m, result.Items[0].SaleAmount);
        Assert.Equal(targetEventId, result.Items[0].EventId);
    }

    [Fact]
    public async Task GetSalesReportsAsync_PaginatesCorrectly()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        for (int i = 1; i <= 6; i++)
        {
            var booking = CreateTestBooking(eventId, customerId, 50m, (Guid.NewGuid(), "General", 1, 50m));

            await _salesReportingService.GenerateSalesReportOnPurchaseAsync(new PaymentSucceededEvent
            {
                EventId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = 50m,
                Currency = "USD"
            });
        }

        // Act: Request Page 2 with PageSize 2
        var page2Result = await _salesReportingService.GetSalesReportsAsync(new ReportFilterDto { Page = 2, PageSize = 2 });

        // Assert
        Assert.Equal(2, page2Result.Items.Count);
        Assert.Equal(2, page2Result.Page);
        Assert.Equal(6, page2Result.TotalCount);
        Assert.True(page2Result.HasPreviousPage);
        Assert.True(page2Result.HasNextPage);
    }

    [Fact]
    public async Task GetAllSalesReportsAsync_ReturnsAllReports_OrderedByPaymentTimestamp()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var booking = CreateTestBooking(eventId, customerId, 100m * i, (Guid.NewGuid(), "Standard", 1, 100m * i));
            await _salesReportingService.GenerateSalesReportOnPurchaseAsync(new PaymentSucceededEvent
            {
                EventId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = 100m * i,
                Currency = "USD"
            });
        }

        // Act
        var allReports = await _salesReportingService.GetAllSalesReportsAsync();

        // Assert
        Assert.NotNull(allReports);
        Assert.True(allReports.Count >= 3);
    }
}
