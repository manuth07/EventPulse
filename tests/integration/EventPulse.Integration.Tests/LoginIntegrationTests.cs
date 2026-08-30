using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EventPulse.Integration.Tests;

public class LoginIntegrationTests
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:7101") };

    private static object RegisterPayload(string email, string password) => new
    {
        firstName = "Integration",
        lastName = "LoginTest",
        phoneNumber = "0771234567",
        countryCode = "LK",
        email,
        password
    };

    [Fact]
    public async Task Login_WithUnverifiedAccount_IsBlocked()
    {
        var email = $"integration.login.unverified.{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123!";

        var register = await _client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, password));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.NotEqual(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"integration.login.wrongpass.{Guid.NewGuid():N}@example.com";
        var password = "SecurePass123!";

        var register = await _client.PostAsJsonAsync("/api/auth/register", RegisterPayload(email, password));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"nonexistent.{Guid.NewGuid():N}@example.com",
            password = "SomePassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingPassword_Returns400()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "someone@example.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_Returns400()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact(Skip = "Blocked by BUG-07: JWT signing key not configured locally/undocumented. Remove Skip once resolved for all environments.")]
    public async Task Login_WithValidVerifiedCredentials_ReturnsJwtToken()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nimal.test01@example.com",
            password = "SecurePass123!"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}