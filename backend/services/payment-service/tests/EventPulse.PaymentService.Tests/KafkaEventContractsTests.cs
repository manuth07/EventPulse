using System.Text.Json;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Configuration;

namespace EventPulse.PaymentService.Tests;

public class KafkaEventContractsTests
{
    [Fact]
    public void PaymentSucceededEvent_Serialization_RoundTripsCorrectly()
    {
        // Arrange
        var evt = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            EventVersion = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-SUCC1",
            CustomerId = Guid.NewGuid(),
            Amount = 14500.50m,
            Currency = "lkr"
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<PaymentSucceededEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventId, deserialized.EventId);
        Assert.Equal(1, deserialized.EventVersion);
        Assert.Equal(evt.OccurredAtUtc, deserialized.OccurredAtUtc);
        Assert.Equal(evt.PaymentId, deserialized.PaymentId);
        Assert.Equal(evt.BookingId, deserialized.BookingId);
        Assert.Equal("EP-2026-SUCC1", deserialized.BookingReference);
        Assert.Equal(evt.CustomerId, deserialized.CustomerId);
        Assert.Equal(14500.50m, deserialized.Amount);
        Assert.Equal("lkr", deserialized.Currency);
    }

    [Fact]
    public void PaymentSucceededEvent_Defaults_HaveUniqueEventId_VersionOne_AndRecentTimestamp()
    {
        // Act
        var evt1 = new PaymentSucceededEvent();
        var evt2 = new PaymentSucceededEvent();

        // Assert
        Assert.NotEqual(Guid.Empty, evt1.EventId);
        Assert.NotEqual(Guid.Empty, evt2.EventId);
        Assert.NotEqual(evt1.EventId, evt2.EventId);
        Assert.Equal(1, evt1.EventVersion);
        Assert.True(evt1.OccurredAtUtc <= DateTimeOffset.UtcNow);
        Assert.True(evt1.OccurredAtUtc >= DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void PaymentSucceededEvent_EventIdIsDistinctFromPaymentId()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var evt = new PaymentSucceededEvent
        {
            PaymentId = paymentId
        };

        // Assert
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.NotEqual(evt.PaymentId, evt.EventId);
    }

    [Fact]
    public void PaymentFailedEvent_Serialization_RoundTripsCorrectly_WithFailureDetails()
    {
        // Arrange
        var evt = new PaymentFailedEvent
        {
            EventId = Guid.NewGuid(),
            EventVersion = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-FAIL1",
            CustomerId = Guid.NewGuid(),
            Amount = 5000.00m,
            Currency = "lkr",
            FailureCode = "card_declined",
            FailureReason = "Insufficient funds"
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<PaymentFailedEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventId, deserialized.EventId);
        Assert.Equal(1, deserialized.EventVersion);
        Assert.Equal(evt.OccurredAtUtc, deserialized.OccurredAtUtc);
        Assert.Equal(evt.PaymentId, deserialized.PaymentId);
        Assert.Equal(evt.BookingId, deserialized.BookingId);
        Assert.Equal("EP-2026-FAIL1", deserialized.BookingReference);
        Assert.Equal(evt.CustomerId, deserialized.CustomerId);
        Assert.Equal(5000.00m, deserialized.Amount);
        Assert.Equal("lkr", deserialized.Currency);
        Assert.Equal("card_declined", deserialized.FailureCode);
        Assert.Equal("Insufficient funds", deserialized.FailureReason);
    }

    [Fact]
    public void PaymentFailedEvent_Serialization_HandlesNullFailureDetails()
    {
        // Arrange
        var evt = new PaymentFailedEvent
        {
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-FAIL2",
            CustomerId = Guid.NewGuid(),
            Amount = 2500.00m,
            Currency = "usd",
            FailureCode = null,
            FailureReason = null
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<PaymentFailedEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.FailureCode);
        Assert.Null(deserialized.FailureReason);
    }

    [Fact]
    public void PaymentFailedEvent_EventIdIsDistinctFromPaymentId()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var evt = new PaymentFailedEvent
        {
            PaymentId = paymentId
        };

        // Assert
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.NotEqual(evt.PaymentId, evt.EventId);
    }

    [Fact]
    public void KafkaTopics_Constants_HaveExpectedValues()
    {
        Assert.Equal("payment-succeeded", KafkaTopics.PaymentSucceeded);
        Assert.Equal("payment-failed", KafkaTopics.PaymentFailed);
    }

    [Fact]
    public void KafkaOptions_Defaults_MatchExpectedTopicsAndBroker()
    {
        var options = new KafkaOptions();

        Assert.Equal("localhost:9092", options.BootstrapServers);
        Assert.NotNull(options.Topics);
        Assert.Equal(KafkaTopics.PaymentSucceeded, options.Topics.PaymentSucceeded);
        Assert.Equal(KafkaTopics.PaymentFailed, options.Topics.PaymentFailed);
    }

    [Fact]
    public void KafkaOptions_BindsFromConfiguration_WithCustomAndEnvironmentOverrides()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Kafka:BootstrapServers", "broker.internal:9094"},
            {"Kafka:Topics:PaymentSucceeded", "custom-payment-succeeded"},
            {"Kafka:Topics:PaymentFailed", "custom-payment-failed"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var options = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("broker.internal:9094", options.BootstrapServers);
        Assert.Equal("custom-payment-succeeded", options.Topics.PaymentSucceeded);
        Assert.Equal("custom-payment-failed", options.Topics.PaymentFailed);
    }
}
