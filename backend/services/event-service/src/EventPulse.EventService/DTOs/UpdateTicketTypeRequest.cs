using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Request body for an Organizer to update an existing ticket type.
/// TicketTypeId and EventId are taken from the route, never the body.
/// EventId association is immutable — a ticket type cannot be moved to a different event.
/// </summary>
public class UpdateTicketTypeRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(1, 100_000, ErrorMessage = "Total quantity must be at least 1.")]
    public int Capacity { get; set; }

    /// <summary>
    /// Total quantity of tickets. Alias for Capacity (EP-399).
    /// </summary>
    public int? TotalQuantity
    {
        get => Capacity;
        set
        {
            if (value.HasValue && Capacity == 0)
            {
                Capacity = value.Value;
            }
        }
    }
}