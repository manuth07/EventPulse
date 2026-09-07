using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Request body for an Administrator to review (approve or reject) a Pending event.
/// </summary>
public class ReviewEventRequest
{
    /// <summary>
    /// Optional reviewer notes or rejection reason.
    /// Required when rejecting, recommended when approving.
    /// </summary>
    [StringLength(1000)]
    public string? Notes { get; set; }
}
