using Microsoft.AspNetCore.Mvc;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Services;

namespace EventPulse.EventService.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IEventInventoryService _inventoryService;

    public InventoryController(IEventInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Reserves inventory for a pending checkout hold.
    /// </summary>
    [HttpPost("reserve")]
    public async Task<IActionResult> ReserveInventory(
        Guid eventId,
        [FromBody] ReserveInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.ReserveInventoryAsync(eventId, request, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Commits inventory when a booking payment succeeds.
    /// </summary>
    [HttpPost("commit")]
    public async Task<IActionResult> CommitInventory(
        Guid eventId,
        [FromBody] CommitInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.CommitInventoryAsync(eventId, request, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Releases inventory holds when checkout expires or payment fails.
    /// </summary>
    [HttpPost("release")]
    public async Task<IActionResult> ReleaseInventory(
        Guid eventId,
        [FromBody] ReleaseInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.ReleaseInventoryAsync(eventId, request, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Restores inventory when a booking or ticket is cancelled/refunded.
    /// </summary>
    [HttpPost("restore")]
    public async Task<IActionResult> RestoreInventory(
        Guid eventId,
        [FromBody] RestoreInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.RestoreInventoryAsync(eventId, request, cancellationToken);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }
}
