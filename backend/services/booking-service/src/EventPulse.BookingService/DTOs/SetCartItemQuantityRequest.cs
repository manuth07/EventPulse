using System.ComponentModel.DataAnnotations;

namespace EventPulse.BookingService.DTOs;

public class SetCartItemQuantityRequest
{
    [Range(0, 100_000)]
    public int Quantity { get; set; }
}
