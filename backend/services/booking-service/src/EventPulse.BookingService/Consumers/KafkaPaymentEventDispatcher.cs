using System.Text.Json;
using EventPulse.BookingService.Events;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.BookingService.Consumers;

public class KafkaPaymentEventDispatcher : IKafkaPaymentEventDispatcher
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly IPaymentSucceededEventHandler _succeededHandler;
    private readonly IPaymentFailedEventHandler _failedHandler;
    private readonly IPaymentRefundedEventHandler _refundedHandler;
    private readonly ILogger<KafkaPaymentEventDispatcher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaPaymentEventDispatcher(
        IOptions<KafkaOptions> kafkaOptions,
        IPaymentSucceededEventHandler succeededHandler,
        IPaymentFailedEventHandler failedHandler,
        IPaymentRefundedEventHandler refundedHandler,
        ILogger<KafkaPaymentEventDispatcher> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _succeededHandler = succeededHandler;
        _failedHandler = failedHandler;
        _refundedHandler = refundedHandler;
        _logger = logger;
    }

    public async Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Received empty or whitespace payload from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable("Empty or whitespace payload");
        }

        if (string.Equals(topic, _kafkaOptions.Topics.PaymentSucceeded, StringComparison.OrdinalIgnoreCase))
        {
            PaymentSucceededEvent? succeededEvent;
            try
            {
                succeededEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(value, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize PaymentSucceededEvent from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable($"Malformed JSON payload: {ex.Message}", ex.GetType().Name);
            }

            if (succeededEvent == null)
            {
                _logger.LogError("Deserialized PaymentSucceededEvent was null from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable("Deserialized PaymentSucceededEvent was null");
            }

            _logger.LogInformation("Received PaymentSucceededEvent {EventId} for Booking {BookingId} from {Topic} (Key: {Key})",
                succeededEvent.EventId, succeededEvent.BookingId, topic, key);

            try
            {
                await _succeededHandler.HandleAsync(succeededEvent, cancellationToken);
                _logger.LogInformation("Successfully handled PaymentSucceededEvent {EventId} for Booking {BookingId}",
                    succeededEvent.EventId, succeededEvent.BookingId);
                return EventDispatchResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error handling PaymentSucceededEvent {EventId} for Booking {BookingId}",
                    succeededEvent.EventId, succeededEvent.BookingId);
                return EventDispatchResult.Retryable(ex.Message, ex.GetType().Name);
            }
        }
        else if (string.Equals(topic, _kafkaOptions.Topics.PaymentFailed, StringComparison.OrdinalIgnoreCase))
        {
            PaymentFailedEvent? failedEvent;
            try
            {
                failedEvent = JsonSerializer.Deserialize<PaymentFailedEvent>(value, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize PaymentFailedEvent from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable($"Malformed JSON payload: {ex.Message}", ex.GetType().Name);
            }

            if (failedEvent == null)
            {
                _logger.LogError("Deserialized PaymentFailedEvent was null from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable("Deserialized PaymentFailedEvent was null");
            }

            _logger.LogInformation("Received PaymentFailedEvent {EventId} for Booking {BookingId} from {Topic} (Key: {Key})",
                failedEvent.EventId, failedEvent.BookingId, topic, key);

            try
            {
                await _failedHandler.HandleAsync(failedEvent, cancellationToken);
                _logger.LogInformation("Successfully handled PaymentFailedEvent {EventId} for Booking {BookingId}",
                    failedEvent.EventId, failedEvent.BookingId);
                return EventDispatchResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error handling PaymentFailedEvent {EventId} for Booking {BookingId}",
                    failedEvent.EventId, failedEvent.BookingId);
                return EventDispatchResult.Retryable(ex.Message, ex.GetType().Name);
            }
        }
        else if (string.Equals(topic, _kafkaOptions.Topics.PaymentRefunded, StringComparison.OrdinalIgnoreCase))
        {
            PaymentRefundedEvent? refundedEvent;
            try
            {
                refundedEvent = JsonSerializer.Deserialize<PaymentRefundedEvent>(value, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize PaymentRefundedEvent from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable($"Malformed JSON payload: {ex.Message}", ex.GetType().Name);
            }

            if (refundedEvent == null)
            {
                _logger.LogError("Deserialized PaymentRefundedEvent was null from topic {Topic} with key {Key}", topic, key);
                return EventDispatchResult.NonRetryable("Deserialized PaymentRefundedEvent was null");
            }

            _logger.LogInformation("Received PaymentRefundedEvent {RefundId} for Booking {BookingId} from {Topic} (Key: {Key})",
                refundedEvent.RefundId, refundedEvent.BookingId, topic, key);

            try
            {
                await _refundedHandler.HandleAsync(refundedEvent, cancellationToken);
                _logger.LogInformation("Successfully handled PaymentRefundedEvent {RefundId} for Booking {BookingId}",
                    refundedEvent.RefundId, refundedEvent.BookingId);
                return EventDispatchResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error handling PaymentRefundedEvent {RefundId} for Booking {BookingId}",
                    refundedEvent.RefundId, refundedEvent.BookingId);
                return EventDispatchResult.Retryable(ex.Message, ex.GetType().Name);
            }
        }
        else
        {
            _logger.LogWarning("Unknown or unconfigured topic {Topic} received with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable($"Unknown or unconfigured topic {topic}");
        }
    }
}
