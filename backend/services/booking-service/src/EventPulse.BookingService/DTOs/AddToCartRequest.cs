using System.ComponentModel.DataAnnotations;

namespace EventPulse.BookingService.DTOs;

public class AddToCartRequest
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid TicketTypeId { get; set; }

    [Range(1, 100_000)]
    public int Quantity { get; set; }
}