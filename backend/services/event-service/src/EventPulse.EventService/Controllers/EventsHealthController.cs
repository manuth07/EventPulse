using Microsoft.AspNetCore.Mvc;

namespace EventPulse.EventService.Controllers;

/// <summary>
/// Confirms the Event Service is running and reachable.
/// This controller is temporary scaffolding — replace with real
/// event CRUD endpoints as the feature is implemented.
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsHealthController : ControllerBase
{
    /// <summary>
    /// Health probe routed via: GET /api/events/health → Gateway → EventService
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "EventPulse.EventService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
