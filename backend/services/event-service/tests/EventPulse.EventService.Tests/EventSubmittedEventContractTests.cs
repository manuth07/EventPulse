using System.Text.Json;
using EventPulse.Contracts.Kafka;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventSubmittedEventContractTests
{
    [Fact]
    public void EventSubmittedEvent_Serialization_RoundTripsCorrectly()
    {
        // Arrange
        var evt = new EventSubmittedEvent
        {
            EventMessageId = Guid.NewGuid(),
            EventVersion = 1,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            EventId = Guid.NewGuid(),
            EventTitle = "Annual Developer Summit 2026",
            OrganizerId = Guid.NewGuid()
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<EventSubmittedEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventMessageId, deserialized.EventMessageId);
        Assert.Equal(1, deserialized.EventVersion);
        Assert.Equal(evt.SubmittedAtUtc, deserialized.SubmittedAtUtc);
        Assert.Equal(evt.EventId, deserialized.EventId);
        Assert.Equal("Annual Developer Summit 2026", deserialized.EventTitle);
        Assert.Equal(evt.OrganizerId, deserialized.OrganizerId);
    }

    [Fact]
    public void EventSubmittedEvent_Defaults_HaveUniqueMessageId_VersionOne_AndRecentUtcTimestamp()
    {
        // Act
        var evt1 = new EventSubmittedEvent();
        var evt2 = new EventSubmittedEvent();

        // Assert
        Assert.NotEqual(Guid.Empty, evt1.EventMessageId);
        Assert.NotEqual(Guid.Empty, evt2.EventMessageId);
        Assert.NotEqual(evt1.EventMessageId, evt2.EventMessageId);
        Assert.Equal(1, evt1.EventVersion);
        Assert.True(evt1.SubmittedAtUtc <= DateTimeOffset.UtcNow);
        Assert.True(evt1.SubmittedAtUtc >= DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(string.Empty, evt1.EventTitle);
        Assert.Equal(Guid.Empty, evt1.EventId);
        Assert.Equal(Guid.Empty, evt1.OrganizerId);
    }

    [Fact]
    public void EventSubmittedEvent_EventMessageIdIsDistinctFromDomainEventId()
    {
        // Arrange
        var domainEventId = Guid.NewGuid();
        var evt = new EventSubmittedEvent
        {
            EventId = domainEventId
        };

        // Assert
        Assert.NotEqual(Guid.Empty, evt.EventMessageId);
        Assert.NotEqual(evt.EventId, evt.EventMessageId);
        Assert.Equal(domainEventId, evt.EventId);
    }

    [Fact]
    public void EventSubmittedEvent_ConsumerProducerCompatibility_DeserializesCamelCaseJson()
    {
        // Arrange: Producer emits camelCase JSON (or consumer reads camelCase JSON with PropertyNameCaseInsensitive)
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var evt = new EventSubmittedEvent
        {
            EventMessageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EventVersion = 1,
            SubmittedAtUtc = DateTimeOffset.Parse("2026-10-07T12:00:00Z"),
            EventId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            EventTitle = "Spring Music Gala",
            OrganizerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")
        };

        var json = JsonSerializer.Serialize(evt, options);

        // Consumer options matching EventPulse consumer pattern (PropertyNameCaseInsensitive = true)
        var consumerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var deserialized = JsonSerializer.Deserialize<EventSubmittedEvent>(json, consumerOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventMessageId, deserialized.EventMessageId);
        Assert.Equal(evt.EventVersion, deserialized.EventVersion);
        Assert.Equal(evt.SubmittedAtUtc, deserialized.SubmittedAtUtc);
        Assert.Equal(evt.EventId, deserialized.EventId);
        Assert.Equal("Spring Music Gala", deserialized.EventTitle);
        Assert.Equal(evt.OrganizerId, deserialized.OrganizerId);
    }

    [Fact]
    public void EventSubmittedEvent_ConsumerProducerCompatibility_DeserializesPascalCaseJson()
    {
        // Arrange: Producer emits default PascalCase JSON (e.g. OutboxWriter PropertyNamingPolicy = null)
        var rawJson = """
        {
            "EventMessageId": "11111111-1111-1111-1111-111111111111",
            "EventVersion": 1,
            "SubmittedAtUtc": "2026-10-07T14:30:00+00:00",
            "EventId": "22222222-2222-2222-2222-222222222222",
            "EventTitle": "AI Innovation Expo",
            "OrganizerId": "33333333-3333-3333-3333-333333333333"
        }
        """;

        var consumerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        var deserialized = JsonSerializer.Deserialize<EventSubmittedEvent>(rawJson, consumerOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), deserialized.EventMessageId);
        Assert.Equal(1, deserialized.EventVersion);
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T14:30:00+00:00"), deserialized.SubmittedAtUtc);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), deserialized.EventId);
        Assert.Equal("AI Innovation Expo", deserialized.EventTitle);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), deserialized.OrganizerId);
    }
}
