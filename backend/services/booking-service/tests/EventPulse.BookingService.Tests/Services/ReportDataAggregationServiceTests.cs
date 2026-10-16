using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// EP-377: Unit tests for ReportDataAggregationService under US-39 (Generate Dynamic Reports).
/// Covers:
/// 1. Zero-valued metrics for empty database / query results without errors.
/// 2. Date-range boundary validation (rejects From > To).
/// 3. Bookings Report canonical metrics with known test data (confirmed, cancelled, pending, failed).
/// 4. Cancellation rate and ticket churn calculation rules.
/// 5. Revenue Report aggregation including full and partial refund adjustments.
/// 6. Multi-currency strict segregation (USD vs LKR never mixed into single sum).
/// 7. Platform Activity Summary integration across bookings, revenue, unique customers, and active events.
/// 8. Dimensional filtering by date range, event ID, and organizer ID.
/// </summary>
public class ReportDataAggregationServiceTests
{
    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    // =========================================================================
    // 1. Empty Results Return Zero Values Instead of Throwing
    // =========================================================================

    [Fact]
    public async Task GetBookingsReportData_WhenDatabaseEmpty_ReturnsZeroValuedDto()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var result = await service.GetBookingsReportDataAsync(new BookingsReportQueryParameters());

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalBookings);
        Assert.Equal(0, result.ConfirmedBookings);
        Assert.Equal(0, result.CancelledBookings);
        Assert.Equal(0, result.PendingPaymentBookings);
        Assert.Equal(0, result.PaymentFailedBookings);
        Assert.Equal(0, result.GrossTicketsSold);
        Assert.Equal(0, result.ActiveTicketsBooked);
        Assert.Equal(0, result.CancelledTickets);
        Assert.Equal(0.0m, result.CancellationRate);
        Assert.Equal(0.0m, result.TicketCancellationRate);
        Assert.Equal(0.0m, result.AverageTicketsPerBooking);
        Assert.Empty(result.EventsBreakdown);
        Assert.Empty(result.TicketTypesBreakdown);
    }

    [Fact]
    public async Task GetRevenueReportData_WhenDatabaseEmpty_ReturnsZeroValuedDto()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var result = await service.GetRevenueReportDataAsync(new RevenueReportQueryParameters());

        Assert.NotNull(result);
        Assert.Equal(0m, result.GrossRevenue);
        Assert.Equal(0m, result.RefundedAmount);
        Assert.Equal(0m, result.NetRevenue);
        Assert.Equal(0, result.SuccessfulPurchaseCount);
        Assert.Equal(0m, result.AverageOrderValue);
        Assert.Equal(0m, result.RefundRate);
        Assert.Equal(0, result.FullRefundCount);
        Assert.Equal(0, result.PartialRefundCount);
        Assert.Empty(result.CurrencyBreakdown);
        Assert.Empty(result.EventsBreakdown);
        Assert.Empty(result.OrganizersBreakdown);
    }

    [Fact]
    public async Task GetPlatformActivityReportData_WhenDatabaseEmpty_ReturnsZeroValuedDto()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var result = await service.GetPlatformActivityReportDataAsync(new PlatformActivityReportQueryParameters());

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalBookings);
        Assert.Equal(0, result.ConfirmedBookings);
        Assert.Equal(0, result.CancelledBookings);
        Assert.Equal(0, result.GrossTicketsSold);
        Assert.Equal(0, result.ActiveTicketsBooked);
        Assert.Equal(0m, result.PrimaryGrossRevenue);
        Assert.Equal(0m, result.PrimaryNetRevenue);
        Assert.Equal(0, result.TotalUniqueCustomers);
        Assert.Equal(0, result.TotalActiveEvents);
    }

    // =========================================================================
    // 2. Date Boundary Validation
    // =========================================================================

    [Fact]
    public async Task DateRangeValidation_WhenFromIsAfterTo_ThrowsArgumentException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var query = new BookingsReportQueryParameters
        {
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetBookingsReportDataAsync(query));

        Assert.Contains("start date must be earlier", ex.Message);
    }

    // =========================================================================
    // 3. Bookings Report Canonical Metrics & Cancellation Churn
    // =========================================================================

    [Fact]
    public async Task GetBookingsReportData_CalculatesCorrectTotalsAndExcludesPendingHolds()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        // 1. Confirmed booking 1: 3 tickets
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-CONF-1",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = ticketTypeId, TicketName = "General", Quantity = 3, UnitPrice = 50m, Subtotal = 150m }
            }
        });

        // 2. Confirmed booking 2: 2 tickets
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-CONF-2",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = ticketTypeId, TicketName = "General", Quantity = 2, UnitPrice = 50m, Subtotal = 100m }
            }
        });

        // 3. Cancelled booking: 1 ticket (previously confirmed)
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-CANC-1",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-3),
            CancelledAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = ticketTypeId, TicketName = "General", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }
            }
        });

        // 4. In-flight Pending Payment hold (15-min reservation hold) - MUST NOT BE COUNTED IN TOTAL BOOKINGS OR REVENUE
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-PEND-1",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = ticketTypeId, TicketName = "General", Quantity = 4, UnitPrice = 50m, Subtotal = 200m }
            }
        });

        // 5. Payment Failed checkout attempt - MUST NOT BE COUNTED IN TOTAL BOOKINGS
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-FAIL-1",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.PaymentFailed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = ticketTypeId, TicketName = "General", Quantity = 2, UnitPrice = 50m, Subtotal = 100m }
            }
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetBookingsReportDataAsync(new BookingsReportQueryParameters());

        // Assert:
        // Total completed bookings = 2 Confirmed + 1 Cancelled = 3
        Assert.Equal(3, result.TotalBookings);
        Assert.Equal(2, result.ConfirmedBookings);
        Assert.Equal(1, result.CancelledBookings);

        // Diagnostics
        Assert.Equal(1, result.PendingPaymentBookings);
        Assert.Equal(1, result.PaymentFailedBookings);

        // Tickets:
        // Active = 3 + 2 = 5
        // Cancelled = 1
        // Gross = 5 + 1 = 6
        Assert.Equal(5, result.ActiveTicketsBooked);
        Assert.Equal(1, result.CancelledTickets);
        Assert.Equal(6, result.GrossTicketsSold);

        // Churn rates:
        // Cancellation Rate = (1 / 3) * 100 = 33.33%
        Assert.Equal(33.33m, result.CancellationRate);
        // Ticket Cancellation Rate = (1 / 6) * 100 = 16.67%
        Assert.Equal(16.67m, result.TicketCancellationRate);
        // Average tickets per confirmed booking = 5 / 2 = 2.5
        Assert.Equal(2.5m, result.AverageTicketsPerBooking);

        // Breakdowns
        Assert.Single(result.EventsBreakdown);
        Assert.Equal(eventId, result.EventsBreakdown[0].EventId);
        Assert.Equal(3, result.EventsBreakdown[0].TotalBookings);
        Assert.Equal(5, result.EventsBreakdown[0].ActiveTickets);

        Assert.Single(result.TicketTypesBreakdown);
        Assert.Equal("General", result.TicketTypesBreakdown[0].TicketName);
        Assert.Equal(6, result.TicketTypesBreakdown[0].TotalQuantity);
        Assert.Equal(5, result.TicketTypesBreakdown[0].ActiveQuantity);
        Assert.Equal(1, result.TicketTypesBreakdown[0].CancelledQuantity);
    }

    // =========================================================================
    // 4. Revenue Report Aggregation & Refunds
    // =========================================================================

    [Fact]
    public async Task GetRevenueReportData_CalculatesGrossNetAndRefundsAccurately()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        // 1. Full unrefunded sale: $200
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-100",
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 200m,
            RefundedAmount = 0m,
            RefundStatus = SalesRefundStatus.None,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-2)
        });

        // 2. Fully refunded sale: $100 sale, $100 refunded
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-101",
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 100m,
            RefundedAmount = 100m,
            RefundStatus = SalesRefundStatus.Refunded,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-3)
        });

        // 3. Partially refunded sale: $300 sale, $60 refunded (e.g. 1 of 5 tickets cancelled)
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-102",
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 300m,
            RefundedAmount = 60m,
            RefundStatus = SalesRefundStatus.PartiallyRefunded,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetRevenueReportDataAsync(new RevenueReportQueryParameters());

        // Assert:
        // Gross = 200 + 100 + 300 = 600
        Assert.Equal(600m, result.GrossRevenue);
        // Refunded = 0 + 100 + 60 = 160
        Assert.Equal(160m, result.RefundedAmount);
        // Net = 600 - 160 = 440
        Assert.Equal(440m, result.NetRevenue);

        Assert.Equal(3, result.SuccessfulPurchaseCount);
        // AOV = 600 / 3 = 200
        Assert.Equal(200m, result.AverageOrderValue);
        // Refund Rate = (160 / 600) * 100 = 26.67%
        Assert.Equal(26.67m, result.RefundRate);

        Assert.Equal(1, result.FullRefundCount);
        Assert.Equal(1, result.PartialRefundCount);
        Assert.Equal("USD", result.Currency);

        Assert.Single(result.EventsBreakdown);
        Assert.Equal(eventId, result.EventsBreakdown[0].EventId);
        Assert.Equal(600m, result.EventsBreakdown[0].GrossRevenue);
        Assert.Equal(440m, result.EventsBreakdown[0].NetRevenue);

        Assert.Single(result.OrganizersBreakdown);
        Assert.Equal(organizerId, result.OrganizersBreakdown[0].OrganizerId);
        Assert.Equal(440m, result.OrganizersBreakdown[0].NetRevenue);
    }

    // =========================================================================
    // 5. Strict Multi-Currency Segregation (Never Mix Currencies)
    // =========================================================================

    [Fact]
    public async Task GetRevenueReportData_MultiCurrency_MaintainsStrictIsolation()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        // USD sale: $150
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-USD",
            SaleAmount = 150m,
            RefundedAmount = 0m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        // LKR sale: 100,000 LKR
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-LKR",
            SaleAmount = 100000m,
            RefundedAmount = 5000m,
            Currency = "LKR",
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetRevenueReportDataAsync(new RevenueReportQueryParameters());

        // Assert: Currencies must NOT be summed together into 100,150!
        Assert.Equal(2, result.CurrencyBreakdown.Count);

        var lkrSummary = result.CurrencyBreakdown.First(c => c.Currency == "LKR");
        Assert.Equal(100000m, lkrSummary.GrossRevenue);
        Assert.Equal(5000m, lkrSummary.RefundedAmount);
        Assert.Equal(95000m, lkrSummary.NetRevenue);

        var usdSummary = result.CurrencyBreakdown.First(c => c.Currency == "USD");
        Assert.Equal(150m, usdSummary.GrossRevenue);
        Assert.Equal(0m, usdSummary.RefundedAmount);
        Assert.Equal(150m, usdSummary.NetRevenue);
    }

    // =========================================================================
    // 6. Platform Activity Summary Integration
    // =========================================================================

    [Fact]
    public async Task GetPlatformActivityReportData_CombinesThroughputRevenueAndCustomerCounts()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();

        // Customer A: Booking on Event 1
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-A1",
            CustomerId = customerA,
            EventId = event1,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        });

        // Customer A: Another booking on Event 1
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-A2",
            CustomerId = customerA,
            EventId = event1,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 1 } }
        });

        // Customer B: Booking on Event 2 (Cancelled)
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-B1",
            CustomerId = customerB,
            EventId = event2,
            Status = BookingStatus.Cancelled,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CancelledAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 3 } }
        });

        // Sales Report for Customer A's purchase
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "BK-A1",
            CustomerId = customerA,
            EventId = event1,
            SaleAmount = 250m,
            RefundedAmount = 0m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetPlatformActivityReportDataAsync(new PlatformActivityReportQueryParameters());

        // Assert:
        Assert.Equal(3, result.TotalBookings);
        Assert.Equal(2, result.ConfirmedBookings);
        Assert.Equal(1, result.CancelledBookings);
        Assert.Equal(33.33m, result.CancellationRate);

        Assert.Equal(6, result.GrossTicketsSold);
        Assert.Equal(3, result.ActiveTicketsBooked);
        Assert.Equal(3, result.CancelledTickets);

        Assert.Equal(250m, result.PrimaryGrossRevenue);
        Assert.Equal(250m, result.PrimaryNetRevenue);
        Assert.Equal(1, result.TotalSuccessfulPurchases);

        // Engagement:
        // Customer A and Customer B = 2 unique customers
        Assert.Equal(2, result.TotalUniqueCustomers);
        // Event 1 and Event 2 = 2 active events
        Assert.Equal(2, result.TotalActiveEvents);
    }

    // =========================================================================
    // 7. Date Range and Dimensional Filtering
    // =========================================================================

    [Fact]
    public async Task GetBookingsReportData_WithDateRange_FiltersCorrectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportDataAggregationService(dbContext, clientMock.Object, NullLogger<ReportDataAggregationService>.Instance);

        var insideDate = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var outsideDate = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-INSIDE",
            Status = BookingStatus.Confirmed,
            ConfirmedAt = insideDate,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 4 } }
        });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-OUTSIDE",
            Status = BookingStatus.Confirmed,
            ConfirmedAt = outsideDate,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 10 } }
        });

        await dbContext.SaveChangesAsync();

        // Query for October only
        var query = new BookingsReportQueryParameters
        {
            From = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero)
        };

        var result = await service.GetBookingsReportDataAsync(query);

        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(4, result.ActiveTicketsBooked);
    }
}
