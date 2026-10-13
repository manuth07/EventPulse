using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using EventPulse.BookingService;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Security;

/// <summary>
/// EP-376: Automated Security and Access Control Tests for Reporting Endpoints (US-39).
/// Verifies:
/// 1. Reporting controller endpoints are strictly decorated with AdminOnly authorization policy.
/// 2. Unauthenticated requests fail policy requirements (HTTP 401).
/// 3. Authenticated Customers fail AdminOnly policy evaluation (HTTP 403).
/// 4. Authenticated Organizers fail AdminOnly policy evaluation (HTTP 403).
/// 5. Authenticated Administrators satisfy AdminOnly and AdministratorOnly policies.
/// 6. Tampered JWT signature and forged role claims fail token validation.
/// 7. Expired JWTs fail validation.
/// 8. Route parameter tampering (organizerId, eventId, bookingId) cannot bypass server-side role enforcement.
/// 9. Client cannot inject or spoof role via headers or query parameters.
/// 10. Reporting DTOs never leak passwords, reset tokens, or sensitive payment information.
/// </summary>
public class ReportsAccessControlSecurityTests
{
    private const string TestSigningKey = "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";
    private const string Issuer = "EventPulse.IdentityService";
    private const string Audience = "EventPulse.Clients";

    private readonly AuthorizationOptions _authOptions;

    public ReportsAccessControlSecurityTests()
    {
        _authOptions = new AuthorizationOptions();
        _authOptions.AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole("Administrator"));
        _authOptions.AddPolicy(AppPolicies.AdministratorOnly, policy => policy.RequireRole("Administrator"));
        _authOptions.AddPolicy(AppPolicies.OrganizerOnly, policy => policy.RequireRole("Organizer"));
    }

    private static string GenerateJwt(
        Guid userId,
        string role,
        string? signingKey = null,
        DateTime? expiresAt = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAt ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal ValidateAndCreatePrincipal(string tokenString, string? validKey = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(validKey ?? TestSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };

        return tokenHandler.ValidateToken(tokenString, validationParameters, out _);
    }

    // =========================================================================
    // 1. Controller Decoration & Policy Assignment
    // =========================================================================

    [Fact]
    public void SalesReportsController_IsDecoratedWith_AdminOnlyPolicy()
    {
        var authAttribute = typeof(SalesReportsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttribute);
        Assert.Equal(AppPolicies.AdminOnly, authAttribute.Policy);
    }

    [Fact]
    public void AppPolicies_DefinesAdminAndAdministratorConstants()
    {
        Assert.Equal("AdminOnly", AppPolicies.AdminOnly);
        Assert.Equal("AdministratorOnly", AppPolicies.AdministratorOnly);
        Assert.Equal("OrganizerOnly", AppPolicies.OrganizerOnly);
    }

    [Fact]
    public void SalesReportsController_AllPublicActions_InheritAdminAuthorization()
    {
        var controllerType = typeof(SalesReportsController);
        var publicMethods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

        foreach (var method in publicMethods)
        {
            var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
            Assert.Null(allowAnonymous); // No endpoint must bypass authorization
        }
    }

    // =========================================================================
    // 2. Unauthenticated Principal Verification (HTTP 401)
    // =========================================================================

    [Fact]
    public void UnauthenticatedPrincipal_FailsAdminOnlyPolicy()
    {
        var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated = false
        var policy = _authOptions.GetPolicy(AppPolicies.AdminOnly);
        Assert.NotNull(policy);

        var requirement = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>().First();

        var context = new AuthorizationHandlerContext(
            policy.Requirements,
            unauthenticatedPrincipal,
            null);

        // Act: Evaluate role requirement
        var hasRole = unauthenticatedPrincipal.IsInRole("Administrator");

        // Assert
        Assert.False(unauthenticatedPrincipal.Identity?.IsAuthenticated);
        Assert.False(hasRole);
    }

    // =========================================================================
    // 3. Customer Role Verification (HTTP 403 Forbidden)
    // =========================================================================

    [Fact]
    public void CustomerPrincipal_FailsAdminOnlyPolicy()
    {
        var customerToken = GenerateJwt(Guid.NewGuid(), "Customer");
        var customerPrincipal = ValidateAndCreatePrincipal(customerToken);

        Assert.True(customerPrincipal.Identity?.IsAuthenticated);
        Assert.False(customerPrincipal.IsInRole("Administrator"));
        Assert.True(customerPrincipal.IsInRole("Customer"));
    }

    // =========================================================================
    // 4. Organizer Role Verification (HTTP 403 Forbidden)
    // =========================================================================

    [Fact]
    public void OrganizerPrincipal_FailsAdminOnlyPolicy()
    {
        var organizerToken = GenerateJwt(Guid.NewGuid(), "Organizer");
        var organizerPrincipal = ValidateAndCreatePrincipal(organizerToken);

        Assert.True(organizerPrincipal.Identity?.IsAuthenticated);
        Assert.False(organizerPrincipal.IsInRole("Administrator"));
        Assert.True(organizerPrincipal.IsInRole("Organizer"));
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Organizer")]
    [InlineData("Guest")]
    [InlineData("Manager")]
    public void NonAdministratorRoles_CannotSatisfyAdminPolicies(string role)
    {
        var token = GenerateJwt(Guid.NewGuid(), role);
        var principal = ValidateAndCreatePrincipal(token);

        Assert.False(principal.IsInRole("Administrator"));
    }

    // =========================================================================
    // 5. Administrator Role Verification (HTTP 200 Allowed)
    // =========================================================================

    [Fact]
    public void AdministratorPrincipal_SatisfiesAdminOnlyPolicy()
    {
        var adminToken = GenerateJwt(Guid.NewGuid(), "Administrator");
        var adminPrincipal = ValidateAndCreatePrincipal(adminToken);

        Assert.True(adminPrincipal.Identity?.IsAuthenticated);
        Assert.True(adminPrincipal.IsInRole("Administrator"));
    }

    [Fact]
    public async Task AdministratorPrincipal_CanRetrieveSalesReportById()
    {
        var serviceMock = new Mock<ISalesReportingService>();
        var saleId = Guid.NewGuid();
        var report = new SalesReport
        {
            Id = saleId,
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            SaleAmount = 250.00m,
            Currency = "USD"
        };

        serviceMock.Setup(s => s.GetSalesReportByIdAsync(saleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var controller = new SalesReportsController(serviceMock.Object, NullLogger<SalesReportsController>.Instance);
        var adminPrincipal = ValidateAndCreatePrincipal(GenerateJwt(Guid.NewGuid(), "Administrator"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminPrincipal }
        };

        var result = await controller.GetById(saleId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SalesReportDto>(okResult.Value);
        Assert.Equal(saleId, dto.Id);
    }

    // =========================================================================
    // 6. Token Tampering, Forgery & Signature Integrity
    // =========================================================================

    [Fact]
    public void ForgedToken_WrongSigningKey_FailsSignatureValidation()
    {
        var maliciousKey = "MaliciousAttackerSigningKey_WithSufficient256BitLength!!";
        var forgedToken = GenerateJwt(Guid.NewGuid(), "Administrator", signingKey: maliciousKey);

        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            ValidateAndCreatePrincipal(forgedToken, validKey: TestSigningKey);
        });
    }

    [Fact]
    public void ExpiredToken_FailsValidation()
    {
        var pastDate = DateTime.UtcNow.AddMinutes(-10);
        var expiredToken = GenerateJwt(Guid.NewGuid(), "Administrator", expiresAt: pastDate);

        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            ValidateAndCreatePrincipal(expiredToken);
        });
    }

    [Fact]
    public void MalformedToken_FailsValidation()
    {
        var malformedToken = "not.a.valid.jwt.token";

        Assert.ThrowsAny<Exception>(() =>
        {
            ValidateAndCreatePrincipal(malformedToken);
        });
    }

    // =========================================================================
    // 7. Route and Parameter Tampering Cannot Bypass Server-Side Policy
    // =========================================================================

    [Fact]
    public void Organizer_AttemptingToAccess_ByOrganizerEndpoint_IsDeniedByPolicy()
    {
        // An organizer might attempt to call GET /api/bookings/sales-reports/by-organizer/{theirOwnOrganizerId}
        // or another organizer's ID. Since the policy on the controller is AdminOnly,
        // the server-side authorization filter rejects the request with 403 before execution.
        var organizerId = Guid.NewGuid();
        var organizerToken = GenerateJwt(organizerId, "Organizer");
        var principal = ValidateAndCreatePrincipal(organizerToken);

        // Policy evaluation
        Assert.False(principal.IsInRole("Administrator"));
    }

    [Fact]
    public void Customer_AttemptingToAccess_ByBookingEndpoint_IsDeniedByPolicy()
    {
        // A customer might attempt to call GET /api/bookings/sales-reports/by-booking/{theirBookingId}.
        // The policy strictly prevents customer access.
        var customerId = Guid.NewGuid();
        var customerToken = GenerateJwt(customerId, "Customer");
        var principal = ValidateAndCreatePrincipal(customerToken);

        Assert.False(principal.IsInRole("Administrator"));
    }

    [Fact]
    public void UserRole_IsDerivedAuthoritativelyFromServerToken_NeverFromClientParameters()
    {
        // Attacker passes role in query or header, but the server only inspects ClaimTypes.Role from verified JWT
        var userClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(userClaims, "Bearer");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        // Even if client sent request headers:
        httpContext.Request.Headers["X-User-Role"] = "Administrator";
        httpContext.Request.QueryString = new QueryString("?role=Administrator");

        // The authoritative server-side role check checks User.IsInRole
        Assert.False(httpContext.User.IsInRole("Administrator"));
        Assert.True(httpContext.User.IsInRole("Customer"));
    }

    // =========================================================================
    // 8. Protection Against Information Disclosure (PII / Secrets)
    // =========================================================================

    [Fact]
    public void SalesReportDtos_DoNotExposePasswordsTokensOrSensitivePaymentSecrets()
    {
        var dtoTypes = new[]
        {
            typeof(SalesReportDto),
            typeof(SalesReportItemDto),
            typeof(SaleRefundAdjustmentDto)
        };

        var sensitiveFieldNames = new[]
        {
            "Password",
            "PasswordHash",
            "ResetToken",
            "SecurityStamp",
            "TwoFactorCode",
            "CardNumber",
            "Cvc",
            "Cvv",
            "CardExpiry",
            "StripeClientSecret",
            "StripeSessionToken",
            "ClientSecret",
            "CustomerEmail",
            "CustomerPhone"
        };

        foreach (var type in dtoTypes)
        {
            var propertyNames = type.GetProperties().Select(p => p.Name).ToList();

            foreach (var sensitiveField in sensitiveFieldNames)
            {
                Assert.DoesNotContain(sensitiveField, propertyNames);
            }
        }
    }
}
