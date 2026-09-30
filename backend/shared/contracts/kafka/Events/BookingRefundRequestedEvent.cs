namespace EventPulse.Contracts.Kafka.Events;

public record BookingRefundRequestedEvent(
    Guid RefundRequestId,
    Guid BookingId,
    string BookingReference,
    Guid CustomerId,
    Guid EventId,
    decimal RefundAmount,
    string Reason,
    DateTimeOffset RequestedAt
);
