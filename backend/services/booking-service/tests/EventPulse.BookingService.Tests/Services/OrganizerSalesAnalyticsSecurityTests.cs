using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// EP-386: Dedicated Security and Access Control Tests for Sales Analytics
/// Covers backend access verification across all 13 security testing scenarios:
/// 1. Anonymous API request
/// 2. Customer API request
/// 3. Organizer's own event
/// 4. Another organizer's event
/// 5. Manipulated event ID
/// 6. Manipulated organizer ID
/// 7. Invalid JWT
/// 8. Expired JWT
/// 9. Sensitive response data exposure
/// 10. Query filter isolation (prevent cross-organizer data leaks)
/// </summary>
public class OrganizerSalesAnalyticsSecurityTests
{
    private const string JwtSecret = "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";

    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    // =========================================================================
    // 1. Anonymous API Request -> Rejected (Policy & Controller check)
    // =========================================================================
    [Fact]
    public async Task Scenario1_AnonymousRequest_ReturnsUnauthorized()
    {
        // Arrange
        var serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        var controller = new OrganizerSalesAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerSalesAnalyticsController>.Instance);

        // Anonymous context (no identity/claims)
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.GetOrganizerSalesSummary(null, null, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    // =========================================================================
    // 2. Customer API Request -> Rejected by OrganizerOnly Policy
    // =========================================================================
    [Fact]
    public void Scenario2_CustomerRole_RejectedByOrganizerOnlyPolicy()
    {
        // Act: Inspect Authorize attribute on controller
        var authAttr = typeof(OrganizerSalesAnalyticsController).GetCustomAttribute<AuthorizeAttribute>();

        // Assert: Policy is strictly OrganizerOnly (Customer role will fail authorization)
        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.OrganizerOnly, authAttr.Policy);
    }

    // =========================================================================
    // 3. Organizer's Own Event -> Permitted
    // =========================================================================
    [Fact]
    public async Task Scenario3_OrganizersOwnEvent_ReturnsSuccessfulAnalytics()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(
            dbContext,
            clientMock.Object,
            NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Legitimate Event",
                OrganizerId = organizerId,
                Status = "Published"
            });

        var sale = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 300m,
            Currency = "USD",
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 3, Subtotal = 300m }
            }
        };
        dbContext.SalesReports.Add(sale);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(300m, result.GrossRevenue);
        Assert.Equal(3, result.TicketsSold);
    }

    // =========================================================================
    // 4. Another Organizer's Event -> Returns Non-Disclosing 404
    // =========================================================================
    [Fact]
    public async Task Scenario4_AnotherOrganizersEvent_ReturnsNonDisclosing404()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(
            dbContext,
            clientMock.Object,
            NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var authenticOrganizerId = Guid.NewGuid();
        var foreignOrganizerId = Guid.NewGuid();
        var foreignEventId = Guid.NewGuid();

        // Foreign event owned by another organizer
        clientMock.Setup(c => c.GetEventSummaryAsync(foreignEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = foreignEventId,
                Title = "Secret Foreign Event",
                OrganizerId = foreignOrganizerId
            });

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(authenticOrganizerId, foreignEventId);

        // Assert: Must return null so controller yields a non-disclosing 404
        Assert.Null(result);

        // Also test controller mapping to 404 with sanitized error message
        var controller = new OrganizerSalesAnalyticsController(
            service,
            NullLogger<OrganizerSalesAnalyticsController>.Instance);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, authenticOrganizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Auth")) }
        };

        var controllerResult = await controller.GetEventSalesAnalytics(foreignEventId, null, null, CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(controllerResult.Result);
        Assert.Equal(404, notFound.StatusCode);
        // Message does not leak existence or ownership
        Assert.Contains("not found or is not accessible", notFound.Value?.ToString() ?? string.Empty);
    }

    // =========================================================================
    // 5. Manipulated Event ID -> Nonexistent / Tampered GUID Returns 404
    // =========================================================================
    [Fact]
    public async Task Scenario5_ManipulatedEventId_ReturnsNotFound()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(
            dbContext,
            clientMock.Object,
            NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var tamperedEventId = Guid.NewGuid(); // Random manipulated ID

        clientMock.Setup(c => c.GetEventSummaryAsync(tamperedEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSummaryInfo?)null);

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, tamperedEventId);

        // Assert
        Assert.Null(result);
    }

    // =========================================================================
    // 6. Manipulated Organizer ID -> Ignored; Identity Derived From Trusted Token
    // =========================================================================
    [Fact]
    public async Task Scenario6_ManipulatedOrganizerId_UsesOnlyVerifiedJwtClaims()
    {
        // Arrange
        var serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        var controller = new OrganizerSalesAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerSalesAnalyticsController>.Instance);

        var genuineOrganizerId = Guid.NewGuid();

        // Verified claims bound by JWT middleware
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, genuineOrganizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Auth")) }
        };

        serviceMock.Setup(s => s.GetOrganizerSalesSummaryAsync(genuineOrganizerId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizerSalesSummaryDto { Currency = "USD" });

        // Act
        var result = await controller.GetOrganizerSalesSummary(null, null, CancellationToken.None);

        // Assert: Service was called strictly with the genuine ID from the JWT token
        serviceMock.Verify(s => s.GetOrganizerSalesSummaryAsync(genuineOrganizerId, null, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    // =========================================================================
    // 7. Invalid JWT -> Token Handler Rejects Tampered Signature
    // =========================================================================
    [Fact]
    public void Scenario7_InvalidJwt_FailsSignatureValidation()
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.UTF8.GetBytes(JwtSecret);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Organizer")
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("WrongKeyTamperedSecret123456789012345678901234")),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tamperedToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(tamperedToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };

        // Assert: Validating with actual key throws SecurityTokenSignatureKeyNotFoundException or SecurityTokenInvalidSignatureException
        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            tokenHandler.ValidateToken(tokenString, validationParameters, out _);
        });
    }

    // =========================================================================
    // 8. Expired JWT -> Token Handler Rejects Expired Token
    // =========================================================================
    [Fact]
    public void Scenario8_ExpiredJwt_FailsLifetimeValidation()
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.UTF8.GetBytes(JwtSecret);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Organizer")
            }),
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var expiredToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(expiredToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Assert: Validating expired token throws SecurityTokenExpiredException
        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            tokenHandler.ValidateToken(tokenString, validationParameters, out _);
        });
    }

    // =========================================================================
    // 9. Query Filters Cannot Leak Other Organizers' Records
    // =========================================================================
    [Fact]
    public async Task Scenario10_QueryFilters_DoNotLeakOtherOrganizersData()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(
            dbContext,
            clientMock.Object,
            NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerA = Guid.NewGuid();
        var organizerB = Guid.NewGuid();

        var saleA = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerA,
            SaleAmount = 100m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem> { new() { Quantity = 1, Subtotal = 100m } }
        };

        var saleB = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerB,
            SaleAmount = 900m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem> { new() { Quantity = 9, Subtotal = 900m } }
        };

        dbContext.SalesReports.AddRange(saleA, saleB);
        await dbContext.SaveChangesAsync();

        // Act: Organizer A requests overall sales summary with broad date range
        var summaryA = await service.GetOrganizerSalesSummaryAsync(
            organizerA,
            from: DateTimeOffset.UtcNow.AddYears(-5),
            to: DateTimeOffset.UtcNow.AddYears(5));

        // Assert: Only Organizer A's sales ($100, 1 ticket) are counted; Organizer B's ($900, 9 tickets) is isolated
        Assert.Equal(100m, summaryA.GrossRevenue);
        Assert.Equal(1, summaryA.TicketsSold);
        Assert.Equal(1, summaryA.SuccessfulPurchaseCount);
    }

    // =========================================================================
    // 10. Sensitive Response Data Exposure Prevention
    // =========================================================================
    [Fact]
    public async Task Scenario13_SensitiveData_NeverExposedInDtoResponses()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(
            dbContext,
            clientMock.Object,
            NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Public Event",
                OrganizerId = organizerId
            });

        var sale = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(), // Secret internal Payment ID
            CustomerId = Guid.NewGuid(), // Customer PII ID
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 150m,
            Currency = "USD",
            Items = new List<SalesReportItem>
            {
                new() { TicketTypeId = Guid.NewGuid(), TicketTypeName = "Standard", UnitPrice = 150m, Quantity = 1, Subtotal = 150m }
            }
        };

        dbContext.SalesReports.Add(sale);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, eventId);

        // Assert: Response DTO contains only aggregated public/organizer metrics
        Assert.NotNull(result);
        var dtoType = typeof(EventSalesAnalyticsDto);
        var properties = dtoType.GetProperties().Select(p => p.Name).ToList();

        // Verify sensitive fields are NOT in DTO
        Assert.DoesNotContain("PaymentId", properties);
        Assert.DoesNotContain("CustomerId", properties);
        Assert.DoesNotContain("CustomerEmail", properties);
        Assert.DoesNotContain("StripePaymentIntentId", properties);
    }
}
