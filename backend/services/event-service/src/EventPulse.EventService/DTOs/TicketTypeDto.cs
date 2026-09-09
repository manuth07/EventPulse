namespace EventPulse.EventService.DTOs;

public class TicketTypeDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public DateTime CreatedAt { get; set; }
}