using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

/// <summary>
/// Service contract for Administrator notification queries and state transitions (EP-150 / EP-32).
/// Strictly accessible by users with Administrator role.
/// </summary>
public interface IAdminNotificationService
{
    /// <summary>
    /// Retrieves all administrator notifications ordered with newest first.
    /// Joins with domain Event to provide live review status.
    /// </summary>
    Task<IReadOnlyList<AdminNotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unread notification count for dashboard indicator badges.
    /// </summary>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a specific notification as read.
    /// Returns true if updated, false if notification not found.
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
