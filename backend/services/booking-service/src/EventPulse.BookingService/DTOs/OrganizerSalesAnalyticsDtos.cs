namespace EventPulse.BookingService.DTOs;

public class ReportingPeriodDto
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public class CurrencySalesSummaryDto
{
    public string Currency { get; set; } = "USD";
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int TicketsSold { get; set; }
    public int NetTicketsSold { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
}

public class OrganizerSalesSummaryDto
{
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int TicketsSold { get; set; }
    public int NetTicketsSold { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
    public string Currency { get; set; } = "USD";
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
    public List<CurrencySalesSummaryDto> Currencies { get; set; } = new();
}

public class TicketTypeSalesSummaryDto
{
    public Guid TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public int RefundedQuantity { get; set; }
    public int NetQuantitySold { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
}

public class EventSalesAnalyticsDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int TicketsSold { get; set; }
    public int NetTicketsSold { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
    public string Currency { get; set; } = "USD";
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
    public List<TicketTypeSalesSummaryDto> TicketTypes { get; set; } = new();
}

public class EventSalesSummaryItemDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public decimal GrossRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public int TicketsSold { get; set; }
    public int NetTicketsSold { get; set; }
    public int SuccessfulPurchaseCount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset? LastSaleAtUtc { get; set; }
}
