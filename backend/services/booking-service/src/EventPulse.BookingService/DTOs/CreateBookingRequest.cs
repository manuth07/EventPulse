using System.ComponentModel.DataAnnotations;

namespace EventPulse.BookingService.DTOs;

public class CreateBookingItemRequest
{
    [Required]
    public Guid TicketTypeId { get; set; }

    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; }
}

public class CreateBookingRequest
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateBookingItemRequest> Items { get; set; } = new();
}
