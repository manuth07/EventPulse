using System.ComponentModel.DataAnnotations;

namespace EventPulse.EventService.DTOs;

/// <summary>
/// Request payload for an Organizer submitting an Event Cancellation Request (EP-35 US-15).
/// </summary>
public class SubmitEventCancellationRequest
{
    /// <summary>
    /// Mandatory reason why the event is being requested for cancellation.
    /// </summary>
    [Required(ErrorMessage = "Cancellation reason is required.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Cancellation reason must be between 5 and 1000 characters.")]
    public string Reason { get; set; } = string.Empty;
}
