using Confluent.Kafka;

namespace EventPulse.EventService.Services;

public interface IKafkaMessageProducer
{
    Task<DeliveryResult<string, string>> ProduceAsync(string topic, string key, string payload, CancellationToken cancellationToken = default);
}
