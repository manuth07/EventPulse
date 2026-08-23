using EventPulse.EventService.Models;

namespace EventPulse.EventService.DTOs;

public class EventListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal Price { get; set; }
    public EventStatus Status { get; set; }
}
