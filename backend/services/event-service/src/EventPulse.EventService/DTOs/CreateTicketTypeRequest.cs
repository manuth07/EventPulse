using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Request body for an Organizer to create a ticket type for an eligible event.
/// EventId and OrganizerId are never taken from the body — EventId comes from the route,
/// OrganizerId is derived from the JWT.
/// </summary>
public class CreateTicketTypeRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(1, 100_000)]
    public int Capacity { get; set; }
}