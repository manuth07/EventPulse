using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Consumers;

public class KafkaDeadLetterPublisher : IDeadLetterPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<KafkaDeadLetterPublisher> _logger;
    private readonly bool _ownsProducer;

    public KafkaDeadLetterPublisher(
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<KafkaDeadLetterPublisher> logger,
        IProducer<string, string>? producer = null)
    {
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;

        if (producer != null)
        {
            _producer = producer;
            _ownsProducer = false;
        }
        else
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaOptions.BootstrapServers
            };
            _producer = new ProducerBuilder<string, string>(config).Build();
            _ownsProducer = true;
        }
    }

    public async Task PublishDeadLetterAsync(DeadLetterMessage deadLetter, CancellationToken cancellationToken = default)
    {
        var targetDlqTopic = ResolveDlqTopic(deadLetter.OriginalTopic);
        var json = JsonSerializer.Serialize(deadLetter);
        var message = new Message<string, string>
        {
            Key = deadLetter.MessageKey ?? Guid.NewGuid().ToString(),
            Value = json
        };

        var deliveryResult = await _producer.ProduceAsync(targetDlqTopic, message, cancellationToken);

        _logger.LogWarning(
            "Routed poison/failed message from {OriginalTopic} (partition {OriginalPartition}, offset {OriginalOffset}) to DLQ topic {DlqTopic} (partition {DlqPartition}, offset {DlqOffset}). Reason: {Reason}",
            deadLetter.OriginalTopic, deadLetter.OriginalPartition, deadLetter.OriginalOffset,
            deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value,
            deadLetter.FailureReason);
    }

    private string ResolveDlqTopic(string originalTopic)
    {
        if (string.Equals(originalTopic, _kafkaOptions.Topics.EventSubmitted, StringComparison.OrdinalIgnoreCase))
        {
            return _kafkaOptions.Topics.EventSubmittedDlq;
        }

        return KafkaTopics.GetDeadLetterTopic(originalTopic);
    }

    public void Dispose()
    {
        if (_ownsProducer)
        {
            try
            {
                _producer.Flush(TimeSpan.FromSeconds(5));
                _producer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Kafka DLQ producer");
            }
        }
    }
}
