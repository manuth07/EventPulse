using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.DTOs;

public class SalesReportDto
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizerId { get; set; }
    public decimal SaleAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTimeOffset PaymentTimestampUtc { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetSaleAmount { get; set; }
    public SalesRefundStatus RefundStatus { get; set; }
    public DateTimeOffset? RefundedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<SalesReportItemDto> Items { get; set; } = new();
    public List<SaleRefundAdjustmentDto> RefundAdjustments { get; set; } = new();

    public static SalesReportDto FromEntity(SalesReport report)
    {
        return new SalesReportDto
        {
            Id = report.Id,
            PaymentId = report.PaymentId,
            BookingId = report.BookingId,
            BookingReference = report.BookingReference,
            CustomerId = report.CustomerId,
            EventId = report.EventId,
            OrganizerId = report.OrganizerId,
            SaleAmount = report.SaleAmount,
            Currency = report.Currency,
            PaymentTimestampUtc = report.PaymentTimestampUtc,
            RefundedAmount = report.RefundedAmount,
            NetSaleAmount = report.NetSaleAmount,
            RefundStatus = report.RefundStatus,
            RefundedAtUtc = report.RefundedAtUtc,
            CreatedAtUtc = report.CreatedAtUtc,
            Items = report.Items.Select(i => new SalesReportItemDto
            {
                Id = i.Id,
                TicketTypeId = i.TicketTypeId,
                TicketTypeName = i.TicketTypeName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
                RefundedQuantity = i.RefundedQuantity,
                RefundedAmount = i.RefundedAmount,
                NetSubtotal = i.NetSubtotal
            }).ToList(),
            RefundAdjustments = report.RefundAdjustments.Select(r => new SaleRefundAdjustmentDto
            {
                Id = r.Id,
                RefundId = r.RefundId,
                Amount = r.Amount,
                Reason = r.Reason,
                Status = r.Status,
                AdjustedAtUtc = r.AdjustedAtUtc
            }).ToList()
        };
    }
}

public class SalesReportItemDto
{
    public Guid Id { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public int RefundedQuantity { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetSubtotal { get; set; }
}

public class SaleRefundAdjustmentDto
{
    public Guid Id { get; set; }
    public Guid RefundId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset AdjustedAtUtc { get; set; }
}

/// <summary>
/// Query criteria and pagination parameters for retrieving sales reports collection (EP-405).
/// </summary>
public class ReportFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? EventId { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? Currency { get; set; }
    public Guid? TicketTypeId { get; set; }
    public string? Category { get; set; }
    public string? BookingStatus { get; set; }
    public string? BookingReference { get; set; }
    public string? Search { get; set; }
}

