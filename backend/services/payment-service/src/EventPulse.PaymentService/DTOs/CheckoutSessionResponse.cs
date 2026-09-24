namespace EventPulse.PaymentService.DTOs;

public record CheckoutSessionResponse(
    string SessionId,
    string CheckoutUrl
);
