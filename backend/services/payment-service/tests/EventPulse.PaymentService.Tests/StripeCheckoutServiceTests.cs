using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventPulse.PaymentService.Tests;

public class StripeCheckoutServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<StripeCheckoutService>> _loggerMock;

    public StripeCheckoutServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Stripe:Currency", "usd"},
            {"Stripe:SuccessUrl", "http://localhost:5173/payment-success?session_id={CHECKOUT_SESSION_ID}"},
            {"Stripe:CancelUrl", "http://localhost:5173/payment-cancel"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _loggerMock = new Mock<ILogger<StripeCheckoutService>>();
    }

    [Fact]
    public void BuildSessionOptions_GeneratesValidStripePayload()
    {
        // Arrange
        var service = new StripeCheckoutService(_configuration, _loggerMock.Object);
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-PAY-1234",
            CustomerId = Guid.NewGuid(),
            Amount = 49.99m,
            Currency = "usd"
        };

        // Act
        var options = service.BuildSessionOptions(payment);

        // Assert
        Assert.NotNull(options);
        Assert.Equal("payment", options.Mode);
        Assert.Equal("http://localhost:5173/payment-success?session_id={CHECKOUT_SESSION_ID}", options.SuccessUrl);
        Assert.Equal("http://localhost:5173/payment-cancel", options.CancelUrl);

        Assert.NotNull(options.LineItems);
        Assert.Single(options.LineItems);

        var lineItem = options.LineItems[0];
        Assert.Equal(1, lineItem.Quantity);
        Assert.NotNull(lineItem.PriceData);
        Assert.Equal("usd", lineItem.PriceData.Currency);
        Assert.Equal(4999L, lineItem.PriceData.UnitAmount);
        Assert.Equal("Booking EP-2026-PAY-1234", lineItem.PriceData.ProductData.Name);

        Assert.NotNull(options.Metadata);
        Assert.Equal(payment.BookingId.ToString(), options.Metadata["BookingId"]);
        Assert.Equal("EP-2026-PAY-1234", options.Metadata["BookingReference"]);
        Assert.Equal(payment.Id.ToString(), options.Metadata["PaymentId"]);
    }
}
