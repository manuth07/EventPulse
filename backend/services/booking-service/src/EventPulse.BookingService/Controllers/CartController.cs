using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Services;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize] // Any authenticated user (Customer role expected)
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ILogger<CartController>? _logger;

    public CartController(ICartService cartService, ILogger<CartController>? logger = null)
    {
        _cartService = cartService;
        _logger = logger;
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(customerIdStr, out customerId);
    }

    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var cart = await _cartService.GetActiveCartAsync(customerId, cancellationToken);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var result = await _cartService.AddOrUpdateItemAsync(customerId, request, cancellationToken);

        if (result.IsConflict && result.Conflict != null)
        {
            return Conflict(result.Conflict);
        }

        if (!result.Success)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = result.Error });
        }

        return Ok(result.Cart);
    }

    [HttpPut("items/{ticketTypeId:guid}")]
    public async Task<IActionResult> SetItemQuantity(
        Guid ticketTypeId,
        [FromBody] SetCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var (cart, error) = await _cartService.SetItemQuantityAsync(customerId, ticketTypeId, request.Quantity, cancellationToken);

        if (error != null)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });
        }

        return Ok(cart);
    }

    [HttpDelete("items/{ticketTypeId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid ticketTypeId, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var (cart, error) = await _cartService.RemoveItemAsync(customerId, ticketTypeId, cancellationToken);

        if (error != null)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });
        }

        return Ok(cart);
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        await _cartService.ClearActiveCartAsync(customerId, cancellationToken);
        return Ok(new { message = "Active cart cleared successfully.", items = Array.Empty<object>(), totalTicketCount = 0, totalAmount = 0 });
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteCart(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var success = await _cartService.CompleteActiveCartAsync(customerId, cancellationToken);
        if (!success)
        {
            return BadRequest(new { code = "NOT_FOUND", message = "No active cart found to complete." });
        }

        return Ok(new { message = "Cart marked as completed." });
    }
}