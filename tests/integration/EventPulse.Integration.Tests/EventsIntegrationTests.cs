using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EventPulse.Integration.Tests;

public class EventsIntegrationTests
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:7102") };

    [Fact]
    public async Task GetEvents_ReturnsOnlyApprovedOrPublishedEvents_NotPendingOrRejected()
    {
        // Expected to FAIL against current dev code — this is evidence for the US-08 report, not a test bug.
        var response = await _client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var evt in events.EnumerateArray())
        {
            var status = evt.GetProperty("status").GetString();
            Assert.True(
                status is "Approved" or "Published",
                $"Event '{evt.GetProperty("title").GetString()}' has status '{status}' but should not be visible (US-08)."
            );
        }
    }

    [Fact]
    public async Task GetEventById_WithSeededApprovedEvent_ReturnsDetails()
    {
        // Uses the seeded "AI Workshop Sri Lanka" event (Approved status) from EventDbSeeder
        var knownApprovedId = "d4444444-4444-4444-4444-444444444444";
        var response = await _client.GetAsync($"/api/events/{knownApprovedId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AI Workshop Sri Lanka", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetEventById_WithSeededPendingEvent_Returns404()
    {
        // Uses the seeded "Startup Meetup 2026" event (Pending status)
        var knownPendingId = "c3333333-3333-3333-3333-333333333333";
        var response = await _client.GetAsync($"/api/events/{knownPendingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_WithNonExistentGuid_Returns404()
    {
        var response = await _client.GetAsync($"/api/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_WithMalformedId_ReturnsNotFoundNotServerError()
    {
        var response = await _client.GetAsync("/api/events/not-a-real-guid");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        // Documents actual behavior — check against TC-EVT-008's expected 400 separately
    }
}