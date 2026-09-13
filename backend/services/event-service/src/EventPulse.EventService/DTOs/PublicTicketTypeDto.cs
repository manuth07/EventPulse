namespace EventPulse.EventService.DTOs;

/// <summary>
/// Customer-facing ticket type view for EP-42 — View Ticket Availability.
/// Exposes only what a visitor needs to decide whether to purchase.
/// </summary>
public class PublicTicketTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsSoldOut { get; set; }
}