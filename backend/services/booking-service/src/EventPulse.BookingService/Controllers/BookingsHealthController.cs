using Microsoft.AspNetCore.Mvc;

namespace EventPulse.BookingService.Controllers;

/// <summary>
/// Confirms the Booking Service is running and reachable.
/// This controller is temporary scaffolding — replace with real
/// booking endpoints as the feature is implemented.
/// </summary>
[ApiController]
[Route("api/bookings")]
public class BookingsHealthController : ControllerBase
{
    /// <summary>
    /// Health probe routed via: GET /api/bookings/health → Gateway → BookingService
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "EventPulse.BookingService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
