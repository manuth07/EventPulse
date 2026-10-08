namespace EventPulse.EventService.DTOs;

/// <summary>
/// Customer-facing ticket type view for EP-42 — View Ticket Availability and EP-399 — Remaining Ticket Quantity.
/// Exposes ticket availability and remaining inventory to visitors.
/// </summary>
public class PublicTicketTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsSoldOut { get; set; }
}