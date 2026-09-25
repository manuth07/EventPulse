namespace EventPulse.PaymentService.DTOs;

public class CheckoutSessionResponse
{
    public Guid? PaymentId { get; set; }
    public Guid? BookingId { get; set; }
    public string? BookingReference { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;

    public CheckoutSessionResponse() { }

    public CheckoutSessionResponse(string sessionId, string checkoutUrl)
    {
        SessionId = sessionId;
        CheckoutUrl = checkoutUrl;
    }

    public CheckoutSessionResponse(Guid paymentId, Guid bookingId, string bookingReference, string sessionId, string checkoutUrl)
    {
        PaymentId = paymentId;
        BookingId = bookingId;
        BookingReference = bookingReference;
        SessionId = sessionId;
        CheckoutUrl = checkoutUrl;
    }
}
