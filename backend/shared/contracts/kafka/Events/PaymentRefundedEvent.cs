namespace EventPulse.Contracts.Kafka.Events;

public record PaymentRefundedEvent(
    Guid RefundId,
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Status,
    DateTimeOffset RefundedAt
);
