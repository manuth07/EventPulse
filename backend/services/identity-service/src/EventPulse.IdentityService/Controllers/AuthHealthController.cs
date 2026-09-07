using Microsoft.AspNetCore.Mvc;

namespace EventPulse.IdentityService.Controllers;

/// <summary>
/// Confirms the Identity Service is running and reachable.
/// This controller is temporary scaffolding — replace with real
/// authentication/user endpoints as the feature is implemented.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthHealthController : ControllerBase
{
    /// <summary>
    /// Health probe routed via: GET /api/auth/health → Gateway → IdentityService
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "EventPulse.IdentityService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
