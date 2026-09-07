using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EventPulse.Integration.Tests;

public class EventReviewFlowIntegrationTests : IAsyncLifetime
{
    private const string BaseUrl = "http://localhost:7000";
    private const string OrganizerEmail = "organizer@eventpulse.dev";
    private const string OrganizerPassword = "Organizer123!";
    private const string AdminEmail = "admin@eventpulse.com";
    private const string AdminPassword = "Admin123!";

    private readonly HttpClient _client = new() { BaseAddress = new Uri(BaseUrl) };
    private string? _organizerToken;
    private string? _adminToken;

    public async Task InitializeAsync()
    {
        _organizerToken = await LoginAsync(OrganizerEmail, OrganizerPassword);
        _adminToken = await LoginAsync(AdminEmail, AdminPassword);
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string?> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string? token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token != null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> SubmitPendingEventAsync(string title)
    {
        var fakeImageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent("Reject-flow test event description."), "description" },
            { new StringContent("Test Venue"), "venue" },
            { new StringContent(DateTime.UtcNow.AddDays(30).ToString("O")), "eventDate" },
            { new StringContent("1000"), "price" },
        };
        var poster = new ByteArrayContent(fakeImageBytes);
        poster.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(poster, "image", "poster.jpg");
        var cover = new ByteArrayContent(fakeImageBytes);
        cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(cover, "coverImage", "cover.jpg");

        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, "/api/events", _organizerToken, form));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task RejectEvent_WithValidNotes_Returns200AndPersistsComment()
    {
        var eventId = await SubmitPendingEventAsync($"Reject Flow Test {Guid.NewGuid():N}");
        const string notes = "Please add a detailed schedule and correct the venue capacity.";

        var response = await _client.SendAsync(BuildRequest(
            HttpMethod.Post, $"/api/events/{eventId}/reject", _adminToken,
            JsonContent.Create(new { notes })));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Rejected", body.GetProperty("status").GetString());
        Assert.Equal(notes, body.GetProperty("reviewComment").GetString());
    }

    [Fact]
    public async Task RejectEvent_AlreadyRejected_Returns409()
    {
        var eventId = await SubmitPendingEventAsync($"Double Reject Test {Guid.NewGuid():N}");
        var payload = JsonContent.Create(new { notes = "First rejection reason." });

        var first = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{eventId}/reject", _adminToken, payload));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{eventId}/reject", _adminToken,
            JsonContent.Create(new { notes = "Second attempt." })));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetPendingEventById_WithRealPendingEvent_ReturnsFullDetails()
    {
        var title = $"Real Pending Lookup Test {Guid.NewGuid():N}";
        var eventId = await SubmitPendingEventAsync(title);

        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/events/admin/pending/{eventId}", _adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("organizerId", out _));
    }
}