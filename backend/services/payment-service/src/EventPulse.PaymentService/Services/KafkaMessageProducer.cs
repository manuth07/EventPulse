using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.PaymentService.Services;

public class KafkaMessageProducer : IKafkaMessageProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessageProducer> _logger;
    private readonly bool _ownsProducer;

    public KafkaMessageProducer(
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<KafkaMessageProducer> logger,
        IProducer<string, string>? producer = null)
    {
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
                BootstrapServers = kafkaOptions.Value.BootstrapServers
            };
            _producer = new ProducerBuilder<string, string>(config).Build();
            _ownsProducer = true;
        }
    }

    public Task<DeliveryResult<string, string>> ProduceAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = payload
        };
        return _producer.ProduceAsync(topic, message, cancellationToken);
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
                _logger.LogWarning(ex, "Error while disposing Kafka producer");
            }
        }
    }
}
