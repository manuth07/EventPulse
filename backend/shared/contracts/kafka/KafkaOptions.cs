namespace EventPulse.Contracts.Kafka;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId { get; set; } = "eventpulse-booking-service";
    public KafkaTopicOptions Topics { get; set; } = new();
    public KafkaConsumerOptions Consumer { get; set; } = new();
    public KafkaOutboxOptions Outbox { get; set; } = new();
}

public class KafkaTopicOptions
{
    public string PaymentSucceeded { get; set; } = KafkaTopics.PaymentSucceeded;
    public string PaymentFailed { get; set; } = KafkaTopics.PaymentFailed;
    public string PaymentSucceededDlq { get; set; } = KafkaTopics.PaymentSucceededDlq;
    public string PaymentFailedDlq { get; set; } = KafkaTopics.PaymentFailedDlq;
}

public class KafkaConsumerOptions
{
    public int MaxProcessingAttempts { get; set; } = 3;
    public double RetryBaseDelaySeconds { get; set; } = 1.0;
}

public class KafkaOutboxOptions
{
    public int PollingIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 20;
}
