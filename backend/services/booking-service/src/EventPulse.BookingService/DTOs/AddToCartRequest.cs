using System.ComponentModel.DataAnnotations;

namespace EventPulse.BookingService.DTOs;

public class AddToCartRequest
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid TicketTypeId { get; set; }

    [Range(0, 100_000)]
    public int Quantity { get; set; }

    /// <summary>
    /// If true, represents delta to add/subtract. By default (false), Quantity represents
    /// the DESIRED final absolute quantity in the cart for idempotency and safety.
    /// </summary>
    public bool IsDelta { get; set; } = false;

    /// <summary>
    /// If true and the customer already has an active cart for another event,
    /// automatically clears the existing cart and starts a new one for this event.
    /// </summary>
    public bool ClearExisting { get; set; } = false;
}