using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using EventPulse.EventService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.EventService.Consumers;

/// <summary>
/// Administrator notification handler for EP-150 / EP-32.
/// Persists AdminNotification records idempotently when EventSubmitted events are consumed.
/// </summary>
public class EventSubmittedNotificationHandler : IEventSubmittedNotificationHandler
{
    private readonly EventDbContext _dbContext;
    private readonly ILogger<EventSubmittedNotificationHandler> _logger;

    public EventSubmittedNotificationHandler(
        EventDbContext dbContext,
        ILogger<EventSubmittedNotificationHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(EventSubmittedEvent evt, CancellationToken cancellationToken = default)
    {
        // 1. Idempotency check: verify notification doesn't already exist for this message
        var exists = await _dbContext.AdminNotifications
            .AnyAsync(n => n.EventMessageId == evt.EventMessageId, cancellationToken);

        if (exists)
        {
            _logger.LogInformation(
                "AdminNotification already exists for EventMessageId {MessageId}. Skipping duplicate creation.",
                evt.EventMessageId);
            return;
        }

        // 2. Format notification message and navigation target
        var title = string.IsNullOrWhiteSpace(evt.EventTitle) ? "Untitled Event" : evt.EventTitle.Trim();
        var message = $"New event submitted for review: '{title}'.";
        var navigationTarget = $"/admin/events/pending/{evt.EventId}";

        var notification = new AdminNotification
        {
            Id = Guid.NewGuid(),
            EventMessageId = evt.EventMessageId,
            EventId = evt.EventId,
            EventTitle = title,
            OrganizerId = evt.OrganizerId,
            Message = message,
            NavigationTarget = navigationTarget,
            SubmittedAtUtc = evt.SubmittedAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsRead = false,
            ReadAtUtc = null
        };

        _dbContext.AdminNotifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created AdminNotification {NotificationId} for EventId {EventId} (MessageId: {MessageId})",
            notification.Id, evt.EventId, evt.EventMessageId);
    }
}
