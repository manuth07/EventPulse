using System.Security.Claims;
using EventPulse.PaymentService.Controllers;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.DTOs;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventPulse.PaymentService.Tests;

public class PaymentsControllerTests
{
    private readonly DbContextOptions<PaymentDbContext> _dbOptions;
    private readonly Mock<IBookingServiceClient> _bookingClientMock;
    private readonly Mock<IStripeCheckoutService> _stripeServiceMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;

    public PaymentsControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _bookingClientMock = new Mock<IBookingServiceClient>();
        _stripeServiceMock = new Mock<IStripeCheckoutService>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();
    }

    private PaymentsController CreateController(PaymentDbContext dbContext, Guid? customerId = null, string? token = null)
    {
        var controller = new PaymentsController(
            dbContext,
            _bookingClientMock.Object,
            _stripeServiceMock.Object,
            _loggerMock.Object
        );

        var claims = new List<Claim>();
        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, customerId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        if (!string.IsNullOrEmpty(token))
        {
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task CreateCheckoutSession_WithValidBooking_ReturnsOkAndPersistsPayment()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-TEST";
        var amount = 125.50m;
        var token = "valid_test_jwt";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSummaryDto(bookingId, bookingRef, customerId, amount, "PendingPayment"));

        _stripeServiceMock
            .Setup(s => s.CreateSessionAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSessionResult("cs_test_session_123", "https://checkout.stripe.com/pay/cs_test_session_123"));

        var controller = CreateController(db, customerId, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutSessionResponse>(okResult.Value);
        Assert.Equal("cs_test_session_123", response.SessionId);
        Assert.Equal("https://checkout.stripe.com/pay/cs_test_session_123", response.CheckoutUrl);

        var savedPayment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        Assert.NotNull(savedPayment);
        Assert.Equal(bookingRef, savedPayment.BookingReference);
        Assert.Equal(customerId, savedPayment.CustomerId);
        Assert.Equal(amount, savedPayment.Amount);
        Assert.Equal(PaymentStatus.Pending, savedPayment.Status);
        Assert.Equal("cs_test_session_123", savedPayment.StripeSessionId);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenCustomerIdMismatches_ReturnsForbidden()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var authenticatedUser = Guid.NewGuid();
        var bookingOwner = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "jwt_user_a";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSummaryDto(bookingId, "EP-2026-FAIL", bookingOwner, 100.00m, "PendingPayment"));

        var controller = CreateController(db, authenticatedUser, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);

        var paymentsInDb = await db.Payments.ToListAsync();
        Assert.Empty(paymentsInDb);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenBookingStatusIsNotPendingPayment_ReturnsBadRequest()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "jwt_user";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSummaryDto(bookingId, "EP-2026-CONFIRMED", customerId, 100.00m, "Confirmed"));

        var controller = CreateController(db, customerId, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        var paymentsInDb = await db.Payments.ToListAsync();
        Assert.Empty(paymentsInDb);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenBookingNotFound_ReturnsNotFound()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "jwt_user";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingSummaryDto?)null);

        var controller = CreateController(db, customerId, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var controller = CreateController(db, customerId: null);
        var request = new CreateCheckoutSessionRequest { BookingId = Guid.NewGuid() };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
