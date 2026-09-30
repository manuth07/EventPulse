namespace EventPulse.Contracts.Kafka;

public class DeadLetterMessage
{
    public string OriginalTopic { get; set; } = string.Empty;
    public int OriginalPartition { get; set; }
    public long OriginalOffset { get; set; }
    public string? MessageKey { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset FailedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
