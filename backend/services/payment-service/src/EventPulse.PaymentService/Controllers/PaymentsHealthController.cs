using Microsoft.AspNetCore.Mvc;

namespace EventPulse.PaymentService.Controllers;

/// <summary>
/// Confirms the Payment Service is running and reachable.
/// This controller is temporary scaffolding — replace with real
/// payment endpoints as the feature is implemented.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsHealthController : ControllerBase
{
    /// <summary>
    /// Health probe routed via: GET /api/payments/health → Gateway → PaymentService
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            service = "EventPulse.PaymentService",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
