using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.PaymentService.Services;

public class KafkaPaymentEventPublisher : IPaymentEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<KafkaPaymentEventPublisher> _logger;

    public KafkaPaymentEventPublisher(
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<KafkaPaymentEventPublisher> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishPaymentSucceededAsync(PaymentSucceededEvent evt, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(evt);
        var message = new Message<string, string>
        {
            Key = evt.BookingId.ToString(),
            Value = json
        };

        var topic = _kafkaOptions.Topics.PaymentSucceeded;
        var deliveryResult = await _producer.ProduceAsync(topic, message, ct);

        _logger.LogInformation(
            "Published PaymentSucceededEvent {EventId} for Booking {BookingId} to {Topic} partition {Partition} offset {Offset}",
            evt.EventId, evt.BookingId, deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
    }

    public async Task PublishPaymentFailedAsync(PaymentFailedEvent evt, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(evt);
        var message = new Message<string, string>
        {
            Key = evt.BookingId.ToString(),
            Value = json
        };

        var topic = _kafkaOptions.Topics.PaymentFailed;
        var deliveryResult = await _producer.ProduceAsync(topic, message, ct);

        _logger.LogInformation(
            "Published PaymentFailedEvent {EventId} for Booking {BookingId} to {Topic} partition {Partition} offset {Offset}",
            evt.EventId, evt.BookingId, deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
    }

    public void Dispose()
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
