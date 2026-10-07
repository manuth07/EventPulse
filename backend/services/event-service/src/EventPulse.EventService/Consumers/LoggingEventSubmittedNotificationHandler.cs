using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;

namespace EventPulse.EventService.Consumers;

/// <summary>
/// Baseline notification handler for EP-149.
/// Logs receipt of validated and deduplicated EventSubmittedEvent instances.
/// To be extended/replaced with DB persistence in EP-150.
/// </summary>
public class LoggingEventSubmittedNotificationHandler : IEventSubmittedNotificationHandler
{
    private readonly ILogger<LoggingEventSubmittedNotificationHandler> _logger;

    public LoggingEventSubmittedNotificationHandler(ILogger<LoggingEventSubmittedNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(EventSubmittedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Admin notification triggered for submitted event: EventId={EventId}, Title='{Title}', OrganizerId={OrganizerId}, SubmittedAtUtc={SubmittedAtUtc}",
            evt.EventId, evt.EventTitle, evt.OrganizerId, evt.SubmittedAtUtc);

        return Task.CompletedTask;
    }
}
