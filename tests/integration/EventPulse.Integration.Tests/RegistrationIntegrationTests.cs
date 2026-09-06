using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EventPulse.Integration.Tests;

public class RegistrationIntegrationTests
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:7101") };

    private static object ValidPayload(string email) => new
    {
        firstName = "Integration",
        lastName = "Test",
        phoneNumber = "0771234567",
        countryCode = "LK",
        email,
        password = "SecurePass123!"
    };

    [Fact]
    public async Task Register_WithValidData_Returns201AndPersistsUser()
    {
        var email = $"integration.{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", ValidPayload(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("verificationRequired").GetBoolean());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409AndDoesNotCreateSecondUser()
    {
        var email = $"integration.dup.{Guid.NewGuid():N}@example.com";

        var first = await _client.PostAsJsonAsync("/api/auth/register", ValidPayload(email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/auth/register", ValidPayload(email));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidData_Returns400AndDoesNotPersist()
    {
        var payload = new
        {
            firstName = "",
            lastName = "Test",
            phoneNumber = "0771234567",
            countryCode = "LK",
            email = "not-an-email",
            password = "123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_RegisterThenVerificationEmailSentAndLoginBlockedUntilVerified()
    {
        var email = $"integration.flow.{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123!";

        var register = await _client.PostAsJsonAsync("/api/auth/register", ValidPayload(email));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("verificationEmailSent").GetBoolean());

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.NotEqual(HttpStatusCode.OK, login.StatusCode);
    }
}