namespace EventPulse.BookingService.DTOs;

/// <summary>
/// Query criteria for aggregating the Bookings Report (US-39 / EP-377).
/// </summary>
public class BookingsReportQueryParameters
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? EventId { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? BookingStatus { get; set; }
}

/// <summary>
/// Aggregated data model for the dynamic Bookings Report.
/// </summary>
public class BookingsReportDataDto
{
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int PendingPaymentBookings { get; set; }
    public int PaymentFailedBookings { get; set; }
    public int GrossTicketsSold { get; set; }
    public int ActiveTicketsBooked { get; set; }
    public int CancelledTickets { get; set; }
    public decimal CancellationRate { get; set; }
    public decimal TicketCancellationRate { get; set; }
    public decimal AverageTicketsPerBooking { get; set; }
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
    public List<EventBookingAggregationItemDto> EventsBreakdown { get; set; } = new();
    public List<TicketTypeAggregationItemDto> TicketTypesBreakdown { get; set; } = new();
}

public class EventBookingAggregationItemDto
{
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int ActiveTickets { get; set; }
    public int CancelledTickets { get; set; }
    public int GrossTickets { get; set; }
    public decimal CancellationRate { get; set; }
}

public class TicketTypeAggregationItemDto
{
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int ActiveQuantity { get; set; }
    public int CancelledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalSubtotal { get; set; }
}

/// <summary>
/// Query criteria for aggregating the Revenue Report (US-39 / EP-377).
/// </summary>
public class RevenueReportQueryParameters
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? EventId { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? Currency { get; set; }
}

/// <summary>
/// Aggregated data model for the dynamic Revenue Report.
/// </summary>
public class RevenueReportDataDto
{
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RefundRate { get; set; }
    public int FullRefundCount { get; set; }
    public int PartialRefundCount { get; set; }
    public string Currency { get; set; } = "USD";
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
    public List<CurrencyRevenueSummaryDto> CurrencyBreakdown { get; set; } = new();
    public List<EventRevenueAggregationItemDto> EventsBreakdown { get; set; } = new();
    public List<OrganizerRevenueAggregationItemDto> OrganizersBreakdown { get; set; } = new();
}

public class CurrencyRevenueSummaryDto
{
    public string Currency { get; set; } = "USD";
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal RefundRate { get; set; }
    public int TicketsSold { get; set; }
    public int NetTicketsSold { get; set; }
}

public class EventRevenueAggregationItemDto
{
    public Guid EventId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int PurchaseCount { get; set; }
}

public class OrganizerRevenueAggregationItemDto
{
    public Guid OrganizerId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int PurchaseCount { get; set; }
}

/// <summary>
/// Query criteria for aggregating the Platform Activity Summary Report (US-39 / EP-377).
/// </summary>
public class PlatformActivityReportQueryParameters
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? EventId { get; set; }
    public Guid? OrganizerId { get; set; }
}

/// <summary>
/// Aggregated data model for the Platform Activity Summary Report.
/// </summary>
public class PlatformActivityReportDataDto
{
    // Booking Throughput Metrics
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal CancellationRate { get; set; }
    public int GrossTicketsSold { get; set; }
    public int ActiveTicketsBooked { get; set; }
    public int CancelledTickets { get; set; }
    public decimal TicketCancellationRate { get; set; }
    public decimal AverageTicketsPerBooking { get; set; }
    public int PendingPaymentHolds { get; set; }
    public int PaymentFailedCheckouts { get; set; }

    // Financial Reconciliation Summary
    public string PrimaryCurrency { get; set; } = "USD";
    public decimal PrimaryGrossRevenue { get; set; }
    public decimal PrimaryRefundedAmount { get; set; }
    public decimal PrimaryNetRevenue { get; set; }
    public int TotalSuccessfulPurchases { get; set; }
    public int TotalFullRefunds { get; set; }
    public int TotalPartialRefunds { get; set; }
    public List<CurrencyRevenueSummaryDto> CurrencyBreakdown { get; set; } = new();

    // Platform Engagement Metrics (where supported by authoritative data)
    public int TotalUniqueCustomers { get; set; }
    public int TotalActiveEvents { get; set; }

    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
}
