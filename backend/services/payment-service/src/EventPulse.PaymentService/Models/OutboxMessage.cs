namespace EventPulse.PaymentService.Models;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string MessageKey { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int PublishAttempts { get; set; }

    public string? LastError { get; set; }
}
