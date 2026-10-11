namespace EventPulse.BookingService.Models;

public class SalesReportItem
{
    public Guid Id { get; set; }

    public Guid SalesReportId { get; set; }

    public Guid BookingItemId { get; set; }

    public Guid TicketTypeId { get; set; }

    public string TicketTypeName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }

    public int RefundedQuantity { get; set; } = 0;

    public decimal RefundedAmount { get; set; } = 0m;

    public decimal NetSubtotal => Math.Max(0m, Subtotal - RefundedAmount);

    public SalesReport? SalesReport { get; set; }
}
