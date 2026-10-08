using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Services;

namespace EventPulse.EventService.Controllers;

/// <summary>
/// Administrator endpoints for managing event submission review notifications (EP-150 / EP-32).
/// Strictly restricted to users in the Administrator role.
/// </summary>
[ApiController]
[Route("api/events/admin/notifications")]
[Authorize(Policy = AppPolicies.AdministratorOnly)]
public class AdminNotificationsController : ControllerBase
{
    private readonly IAdminNotificationService _notificationService;
    private readonly ILogger<AdminNotificationsController> _logger;

    public AdminNotificationsController(
        IAdminNotificationService notificationService,
        ILogger<AdminNotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/events/admin/notifications
    /// Retrieves all administrator notifications ordered newest first.
    /// Includes real-time event review status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminNotificationDto>>> GetNotifications(
        CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetNotificationsAsync(cancellationToken);
        return Ok(notifications);
    }

    /// <summary>
    /// GET /api/events/admin/notifications/unread-count
    /// Retrieves count of unread notifications for badge indicators.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _notificationService.GetUnreadCountAsync(cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// PUT /api/events/admin/notifications/{id}/read
    /// Marks an administrator notification as read.
    /// </summary>
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var notificationId))
        {
            return BadRequest(new { message = "Invalid notification ID format." });
        }

        var updated = await _notificationService.MarkAsReadAsync(notificationId, cancellationToken);
        if (!updated)
        {
            return NotFound(new { message = $"Notification with ID '{id}' was not found." });
        }

        return NoContent();
    }
}
