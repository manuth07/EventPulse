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

    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(customerIdStr, out var customerId))
        {
            _logger?.LogWarning("AddToCart: Could not parse CustomerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var (result, error) = await _cartService.AddToCartAsync(customerId, request, cancellationToken);

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(customerIdStr, out var customerId))
        {
            _logger?.LogWarning("GetCart: Could not parse CustomerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var cart = await _cartService.GetCartAsync(customerId, cancellationToken);
        return Ok(cart);
    }
}