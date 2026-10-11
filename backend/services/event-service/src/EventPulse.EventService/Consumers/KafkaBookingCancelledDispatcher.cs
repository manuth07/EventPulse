using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Consumers;

/// <summary>
/// Handles incoming Kafka booking-cancelled events to restore ticket availability idempotently (EP-364).
/// </summary>
public class KafkaBookingCancelledDispatcher : IKafkaBookingCancelledDispatcher
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly EventDbContext _dbContext;
    private readonly IEventInventoryService _inventoryService;
    private readonly ILogger<KafkaBookingCancelledDispatcher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaBookingCancelledDispatcher(
        IOptions<KafkaOptions> kafkaOptions,
        EventDbContext dbContext,
        IEventInventoryService inventoryService,
        ILogger<KafkaBookingCancelledDispatcher> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Received empty or whitespace payload from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable("Empty or whitespace payload");
        }

        var configuredTopic = string.IsNullOrWhiteSpace(_kafkaOptions.Topics.BookingCancelled)
            ? KafkaTopics.BookingCancelled
            : _kafkaOptions.Topics.BookingCancelled;

        if (!string.Equals(topic, configuredTopic, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Received message from unhandled topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable($"Unhandled topic: {topic}");
        }

        BookingCancelledIntegrationEvent? bookingCancelled;
        try
        {
            bookingCancelled = JsonSerializer.Deserialize<BookingCancelledIntegrationEvent>(value, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize BookingCancelledIntegrationEvent from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable($"Malformed JSON payload: {ex.Message}", ex.GetType().Name);
        }

        if (bookingCancelled == null)
        {
            _logger.LogError("Deserialized BookingCancelledIntegrationEvent was null from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable("Deserialized BookingCancelledIntegrationEvent was null");
        }

        // Schema validation
        if (bookingCancelled.BookingId == Guid.Empty || bookingCancelled.EventId == Guid.Empty)
        {
            _logger.LogError("BookingCancelledIntegrationEvent missing mandatory identifiers (BookingId={BookingId}, EventId={EventId})",
                bookingCancelled.BookingId, bookingCancelled.EventId);
            return EventDispatchResult.NonRetryable("Missing mandatory identifiers (BookingId or EventId)");
        }

        _logger.LogInformation(
            "Processing BookingCancelledIntegrationEvent for Booking {BookingId} ({BookingReference}), Event {EventId} from topic {Topic}",
            bookingCancelled.BookingId, bookingCancelled.BookingReference, bookingCancelled.EventId, topic);

        // Deduplication key: Use EventMessageId if provided, otherwise BookingId
        var messageId = bookingCancelled.EventMessageId != Guid.Empty
            ? bookingCancelled.EventMessageId
            : bookingCancelled.BookingId;

        // 1. Idempotency check via ProcessedIntegrationEvents (Inbox)
        var isAlreadyProcessed = await _dbContext.ProcessedIntegrationEvents
            .AnyAsync(e => e.EventId == messageId, cancellationToken);

        if (isAlreadyProcessed)
        {
            _logger.LogInformation(
                "BookingCancelledIntegrationEvent {MessageId} for Booking {BookingId} has already been processed. Skipping duplicate.",
                messageId, bookingCancelled.BookingId);
            return EventDispatchResult.Success();
        }

        // 2. Extract tickets to restore (support both ReleasedTickets and Items for backward compatibility)
        var items = new List<TicketReservationItemDto>();
        if (bookingCancelled.ReleasedTickets != null && bookingCancelled.ReleasedTickets.Count > 0)
        {
            items = bookingCancelled.ReleasedTickets
                .Where(t => t.Quantity > 0)
                .Select(t => new TicketReservationItemDto { TicketTypeId = t.TicketTypeId, Quantity = t.Quantity })
                .ToList();
        }
        else if (bookingCancelled.Items != null && bookingCancelled.Items.Count > 0)
        {
            items = bookingCancelled.Items
                .Where(t => t.Quantity > 0)
                .Select(t => new TicketReservationItemDto { TicketTypeId = t.TicketTypeId, Quantity = t.Quantity })
                .ToList();
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("No ticket items to restore for Booking {BookingId}. Recording as processed.", bookingCancelled.BookingId);
            try
            {
                _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
                {
                    EventId = messageId,
                    EventType = nameof(BookingCancelledIntegrationEvent),
                    Topic = topic,
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
                return EventDispatchResult.Success();
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
            {
                return EventDispatchResult.Success();
            }
        }

        // 3. Delegate to IEventInventoryService
        var cancellationKey = $"booking-{bookingCancelled.BookingId}-cancelled";
        var restoreRequest = new RestoreInventoryRequest
        {
            BookingId = bookingCancelled.BookingId,
            CancellationKey = cancellationKey,
            Items = items
        };

        try
        {
            var restoreResult = await _inventoryService.RestoreInventoryAsync(
                bookingCancelled.EventId,
                restoreRequest,
                cancellationToken);

            if (restoreResult.Success)
            {
                _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
                {
                    EventId = messageId,
                    EventType = nameof(BookingCancelledIntegrationEvent),
                    Topic = topic,
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully restored inventory and recorded BookingCancelledIntegrationEvent {MessageId} for Booking {BookingId}",
                    messageId, bookingCancelled.BookingId);

                return EventDispatchResult.Success();
            }

            // Check if rejected due to policy guard (e.g. event has already started or concluded per EP-361 Section 7.2)
            if (restoreResult.StatusCode == 400 && restoreResult.ErrorMessage?.Contains("already started or concluded", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation(
                    "Inventory restoration for Booking {BookingId} bypassed per approved policy ({Error}). Marking as processed.",
                    bookingCancelled.BookingId, restoreResult.ErrorMessage);

                _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
                {
                    EventId = messageId,
                    EventType = nameof(BookingCancelledIntegrationEvent),
                    Topic = topic,
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
                await _dbContext.SaveChangesAsync(cancellationToken);

                return EventDispatchResult.Success();
            }

            // Non-transient business/resource failure (e.g. 404 Event not found)
            if (restoreResult.StatusCode == 404 || restoreResult.StatusCode == 400)
            {
                _logger.LogWarning(
                    "Non-retryable inventory restoration failure for Booking {BookingId}: {Error}",
                    bookingCancelled.BookingId, restoreResult.ErrorMessage);
                return EventDispatchResult.NonRetryable(restoreResult.ErrorMessage ?? "Inventory restoration rejected");
            }

            // Transient failure
            _logger.LogWarning(
                "Transient inventory restoration failure for Booking {BookingId}: {Error}",
                bookingCancelled.BookingId, restoreResult.ErrorMessage);
            return EventDispatchResult.Retryable(restoreResult.ErrorMessage ?? "Failed to restore inventory");
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogInformation("BookingCancelledEvent {MessageId} was concurrently recorded. Skipping duplicate.", messageId);
            return EventDispatchResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing BookingCancelledEvent {MessageId} for Booking {BookingId}",
                messageId, bookingCancelled.BookingId);
            return EventDispatchResult.Retryable(ex.Message, ex.GetType().Name);
        }
    }
}
