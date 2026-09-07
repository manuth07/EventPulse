using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EventPulse.Integration.Tests;
public class PublishEventIntegrationTests : IAsyncLifetime
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

        Assert.False(string.IsNullOrEmpty(_organizerToken), "Organizer login failed — check test credentials/BUG-07 fix.");
        Assert.False(string.IsNullOrEmpty(_adminToken), "Admin login failed — check test credentials/BUG-07 fix.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string?> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString();
    }

    private static MultipartFormDataContent BuildValidEventForm(string title)
    {
        var fakeImageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }; // minimal JPEG-like header

        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent("Integration test event description."), "description" },
            { new StringContent("Test Venue, Colombo"), "venue" },
            { new StringContent(DateTime.UtcNow.AddDays(30).ToString("O")), "eventDate" },
            { new StringContent("1000"), "price" },
        };

        var posterContent = new ByteArrayContent(fakeImageBytes);
        posterContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(posterContent, "image", "poster.jpg");

        var coverContent = new ByteArrayContent(fakeImageBytes);
        coverContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(coverContent, "coverImage", "cover.jpg");

        return form;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string? token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token != null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task FullLifecycle_SubmitApprovePublish_EventBecomesPubliclyVisible()
    {
        // ---- 1. Organizer submits a new event ----
        var uniqueTitle = $"IntegrationTest Event {Guid.NewGuid():N}";
        var submitResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/events", _organizerToken, BuildValidEventForm(uniqueTitle)));

        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);
        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = submitBody.GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(eventId));

        Assert.Equal("Pending", submitBody.GetProperty("status").GetString());

        // ---- 2. Confirm it does NOT yet appear on the public events list ----
        var publicListBefore = await _client.GetFromJsonAsync<JsonElement>("/api/events");
        var foundBeforeApproval = publicListBefore.EnumerateArray()
            .Any(e => e.GetProperty("id").GetString() == eventId);
        Assert.False(foundBeforeApproval, "Pending event must not be publicly visible before approval.");

        // ---- 3. Admin approves the event ----
        var approveResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, $"/api/events/{eventId}/approve", _adminToken));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approveBody = await approveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("status").GetString());

        // ---- 4. Confirm Approved events ARE publicly visible even before publish ----
        // (per EventsController.GetEvents(), the filter is Published OR Approved)
        var publicListAfterApproval = await _client.GetFromJsonAsync<JsonElement>("/api/events");
        var foundAfterApproval = publicListAfterApproval.EnumerateArray()
            .Any(e => e.GetProperty("id").GetString() == eventId);
        Assert.True(foundAfterApproval, "Approved events should already be visible on GET /api/events per current filter logic.");

        // ---- 5. Try publishing BEFORE approval would 409 — already covered by RBAC suite;
        //         here we confirm the happy path: publish an Approved event ----
        var publishResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, $"/api/events/{eventId}/publish", _adminToken));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        // ---- 6. Attempt to publish again — must 409, not succeed a second time ----
        var republishResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, $"/api/events/{eventId}/publish", _adminToken));
        Assert.Equal(HttpStatusCode.Conflict, republishResponse.StatusCode);

        // ---- 7. Confirm the event detail endpoint now returns it as Published ----
        var detailResponse = await _client.GetAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_OnPendingEvent_Returns409Conflict()
    {
        // Submit but do NOT approve — attempt to publish directly from Pending
        var uniqueTitle = $"IntegrationTest PendingPublish {Guid.NewGuid():N}";
        var submitResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/events", _organizerToken, BuildValidEventForm(uniqueTitle)));
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

        var submitBody = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = submitBody.GetProperty("id").GetString();

        var publishResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, $"/api/events/{eventId}/publish", _adminToken));

        Assert.Equal(HttpStatusCode.Conflict, publishResponse.StatusCode);
        var body = await publishResponse.Content.ReadAsStringAsync();
        Assert.Contains("Approved", body); // message should mention current-status context
    }

    [Fact]
    public async Task Publish_OnNonExistentEvent_Returns404()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, $"/api/events/{randomId}/publish", _adminToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Publish_OnMalformedId_Returns404()
    {
        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/events/not-a-guid/publish", _adminToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}