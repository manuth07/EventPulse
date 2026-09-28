namespace EventPulse.Contracts.Kafka;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId { get; set; } = "eventpulse-booking-service";
    public KafkaTopicOptions Topics { get; set; } = new();
}

public class KafkaTopicOptions
{
    public string PaymentSucceeded { get; set; } = KafkaTopics.PaymentSucceeded;
    public string PaymentFailed { get; set; } = KafkaTopics.PaymentFailed;
}
