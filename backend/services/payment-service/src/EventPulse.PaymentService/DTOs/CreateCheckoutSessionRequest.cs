using System.ComponentModel.DataAnnotations;

namespace EventPulse.PaymentService.DTOs;

public class CreateCheckoutSessionRequest
{
    [Required]
    public Guid BookingId { get; set; }
}
