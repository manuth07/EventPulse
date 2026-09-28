namespace EventPulse.Contracts.Kafka;

public static class KafkaTopics
{
    public const string PaymentSucceeded = "payment-succeeded";
    public const string PaymentFailed = "payment-failed";
    public const string PaymentSucceededDlq = "payment-succeeded-dlq";
    public const string PaymentFailedDlq = "payment-failed-dlq";

    public static string GetDeadLetterTopic(string originalTopic) => $"{originalTopic}-dlq";
}
