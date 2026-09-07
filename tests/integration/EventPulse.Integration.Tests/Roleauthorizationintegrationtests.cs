using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EventPulse.Integration.Tests;

public class RoleAuthorizationIntegrationTests : IAsyncLifetime
{
    private const string BaseUrl = "http://localhost:7000";

    private const string CustomerEmail = "ishaoshadi6@gmail.com";
    private const string CustomerPassword = "Osha12345!";

    private const string OrganizerEmail = "organizer@eventpulse.dev";
    private const string OrganizerPassword = "Organizer123!";

    private const string AdminEmail = "admin@eventpulse.com";
    private const string AdminPassword = "Admin123!"; // matches AdminBootstrap:Password in your user-secrets

    private readonly HttpClient _client = new() { BaseAddress = new Uri(BaseUrl) };

    private string? _customerToken;
    private string? _organizerToken;
    private string? _adminToken;

    public async Task InitializeAsync()
    {
        _customerToken = await LoginAsync(CustomerEmail, CustomerPassword);
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

    // -------------------------------------------------------------------------------
    // GET /api/events/my-submissions — OrganizerOnly
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task MySubmissions_NoToken_Returns401()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/my-submissions", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MySubmissions_CustomerToken_Returns403()
    {
        Assert.NotNull(_customerToken);
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/my-submissions", _customerToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MySubmissions_AdminToken_Returns403()
    {
        Assert.NotNull(_adminToken);
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/my-submissions", _adminToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MySubmissions_OrganizerToken_Returns200()
    {
        Assert.NotNull(_organizerToken);
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/my-submissions", _organizerToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // GET /api/events/admin/pending — AdministratorOnly
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task AdminPending_NoToken_Returns401()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/admin/pending", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminPending_CustomerToken_Returns403()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/admin/pending", _customerToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminPending_OrganizerToken_Returns403()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/admin/pending", _organizerToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminPending_AdminToken_Returns200()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/events/admin/pending", _adminToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // POST /api/events/{id}/approve — AdministratorOnly (random GUID: proves auth ran)
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task ApproveEvent_NoToken_Returns401()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/approve", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveEvent_OrganizerToken_Returns403()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/approve", _organizerToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveEvent_AdminToken_WithRandomId_Returns404NotForbidden()
    {
        // Proves the role check passed — the 404 comes from business logic (event not found),
        // not from authorization, since a wrong-role caller would get 403 before ever reaching here.
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/approve", _adminToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // POST /api/events/{id}/reject — AdministratorOnly
    // NOTE: EventReviewService validates "notes required" BEFORE looking up the event,
    // so an Admin request with no notes returns 409 (Conflict), not 404, even for a
    // random/non-existent id. This is intentional business-rule ordering, not a bug —
    // but it means the 409 here still proves the role check passed.
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task RejectEvent_NoToken_Returns401()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/reject", null,
            JsonContent.Create(new { })));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RejectEvent_CustomerToken_Returns403()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/reject", _customerToken,
            JsonContent.Create(new { })));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RejectEvent_AdminToken_NoNotes_Returns400BadRequest()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/events/{randomId}/reject", _adminToken,
            JsonContent.Create(new { })));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // PUT /api/events/{id}/publish — AdministratorOnly
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task PublishEvent_NoToken_Returns401()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Put, $"/api/events/{randomId}/publish", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublishEvent_CustomerToken_Returns403()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Put, $"/api/events/{randomId}/publish", _customerToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishEvent_AdminToken_WithRandomId_Returns404NotForbidden()
    {
        var randomId = Guid.NewGuid();
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Put, $"/api/events/{randomId}/publish", _adminToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------------
    // GET /api/auth/me — [Authorize] only, any authenticated role
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task Me_NoToken_Returns401()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/auth/me", null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("organizer")]
    [InlineData("admin")]
    public async Task Me_AnyValidRole_Returns200(string role)
    {
        var token = role switch
        {
            "customer" => _customerToken,
            "organizer" => _organizerToken,
            _ => _adminToken
        };
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Critical security check while we're here: no password hash or security stamp leak
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------------
    // POST /api/events (Submit Event) — OrganizerOnly
    // Sends an empty multipart body so a 400 (validation) proves the CORRECT role got
    // past the auth check, while 401/403 prove the wrong role was blocked before that.
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task SubmitEvent_NoToken_Returns401()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, "/api/events", null, new MultipartFormDataContent()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitEvent_CustomerToken_Returns403()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, "/api/events", _customerToken, new MultipartFormDataContent()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitEvent_AdminToken_Returns403()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, "/api/events", _adminToken, new MultipartFormDataContent()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitEvent_OrganizerToken_EmptyBody_Returns400NotForbidden()
    {
        var response = await _client.SendAsync(BuildRequest(HttpMethod.Post, "/api/events", _organizerToken, new MultipartFormDataContent()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}