using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements administrator notification querying and state tracking (EP-150 / EP-32).
/// Computes review state dynamically against the source-of-truth Event domain model.
/// </summary>
public class AdminNotificationService : IAdminNotificationService
{
    private readonly EventDbContext _context;
    private readonly ILogger<AdminNotificationService>? _logger;

    public AdminNotificationService(EventDbContext context, ILogger<AdminNotificationService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdminNotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _context.AdminNotifications
            .AsNoTracking()
            .Include(n => n.Event)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return notifications.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AdminNotifications
            .AsNoTracking()
            .CountAsync(n => !n.IsRead, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.AdminNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger?.LogInformation("Marked notification {NotificationId} as read.", notificationId);
        }

        return true;
    }

    private static AdminNotificationDto MapToDto(AdminNotification n)
    {
        return new AdminNotificationDto
        {
            Id = n.Id,
            EventMessageId = n.EventMessageId,
            EventId = n.EventId,
            EventTitle = n.EventTitle,
            OrganizerId = n.OrganizerId,
            Message = n.Message,
            NavigationTarget = n.NavigationTarget,
            SubmittedAtUtc = n.SubmittedAtUtc,
            CreatedAtUtc = n.CreatedAtUtc,
            IsRead = n.IsRead,
            ReadAtUtc = n.ReadAtUtc,
            ReviewStatus = n.Event?.Status.ToString() ?? EventStatus.Pending.ToString()
        };
    }
}
