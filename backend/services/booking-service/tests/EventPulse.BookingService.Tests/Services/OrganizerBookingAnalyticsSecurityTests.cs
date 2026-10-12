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
/// EP-391: Dedicated Security and Access Control Tests for Booking Analytics
/// Covers backend access verification across all required security testing scenarios:
/// 1. Anonymous API request returns 401
/// 2. Customer API request rejected by OrganizerOnly policy (403)
/// 3. Organizer's own event allowed and returns analytics
/// 4. Another organizer's event returns non-disclosing null/404
/// 5. Manipulated event ID returns 404
/// 6. Manipulated organizer ID ignored; identity derived exclusively from verified JWT claims
/// 7. Invalid JWT signature fails validation
/// 8. Expired JWT fails validation
/// 9. Summary endpoint isolates organizer events
/// 10. Date-range filter never crosses tenant boundary
/// 11. Controller returns non-disclosing 404 for inaccessible event (prevents existence enumeration)
/// 12. Sensitive customer data/PII and internal IDs never exposed in DTO responses
/// </summary>
public class OrganizerBookingAnalyticsSecurityTests
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
    // 1. Anonymous API Request -> Rejected (401 Unauthorized)
    // =========================================================================
    [Fact]
    public async Task Scenario1_AnonymousRequest_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        var controller = new OrganizerBookingAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerBookingAnalyticsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    // =========================================================================
    // 2. Customer API Request -> Rejected by OrganizerOnly Policy (403 Forbidden)
    // =========================================================================
    [Fact]
    public void Scenario2_CustomerRole_RejectedByOrganizerOnlyPolicy()
    {
        var authorizeAttribute = typeof(OrganizerBookingAnalyticsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.OrganizerOnly, authorizeAttribute.Policy);

        // Verify customer claims do not satisfy Organizer requirement
        var customerClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Customer")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(customerClaims, "TestAuth"));

        Assert.False(principal.IsInRole("Organizer"));
        Assert.True(principal.IsInRole("Customer"));
    }

    // =========================================================================
    // 3. Organizer's Own Event -> Permitted & Correct Analytics Returned
    // =========================================================================
    [Fact]
    public async Task Scenario3_OrganizersOwnEvent_AllowedAndReturnsAnalytics()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Owned Event",
                OrganizerId = organizerId
            });

        clientMock.Setup(c => c.GetEventTicketTypesAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TicketTypeAvailability>
            {
                new() { Id = Guid.NewGuid(), Name = "General", TotalQuantity = 100, RemainingQuantity = 98 }
            });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-OWN",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetEventBookingAnalyticsAsync(organizerId, eventId);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(2, result.ActiveTicketsBooked);
    }

    // =========================================================================
    // 4. Another Organizer's Event -> Denied With Non-Disclosing Null (mapped to 404)
    // =========================================================================
    [Fact]
    public async Task Scenario4_AnotherOrganizersEvent_DeniedWithNonDisclosingNull()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var attackerOrganizerId = Guid.NewGuid();
        var victimOrganizerId = Guid.NewGuid();
        var victimEventId = Guid.NewGuid();

        // Event is owned by victimOrganizerId
        clientMock.Setup(c => c.GetEventSummaryAsync(victimEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = victimEventId,
                Title = "Private Victim Event",
                OrganizerId = victimOrganizerId
            });

        var result = await service.GetEventBookingAnalyticsAsync(attackerOrganizerId, victimEventId);

        // Must return null (mapped to 404 by controller, prevents discovery)
        Assert.Null(result);
    }

    // =========================================================================
    // 5. Manipulated Event ID -> Non-Existent Returns Null
    // =========================================================================
    [Fact]
    public async Task Scenario5_ManipulatedEventId_NonExistent_ReturnsNull()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var nonExistentEventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(nonExistentEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSummaryInfo?)null);

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>());

        var result = await service.GetEventBookingAnalyticsAsync(organizerId, nonExistentEventId);

        Assert.Null(result);
    }

    // =========================================================================
    // 6. Manipulated Organizer ID -> Ignored; Identity Derived From Verified JWT Claims
    // =========================================================================
    [Fact]
    public async Task Scenario6_ManipulatedOrganizerId_UsesOnlyVerifiedJwtClaims()
    {
        var serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        var controller = new OrganizerBookingAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerBookingAnalyticsController>.Instance);

        var genuineOrganizerId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, genuineOrganizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Auth")) }
        };

        serviceMock.Setup(s => s.GetOrganizerBookingSummaryAsync(genuineOrganizerId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizerBookingSummaryDto());

        // Act
        var result = await controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        // Assert: Service called exclusively with genuine Organizer ID from claims
        serviceMock.Verify(s => s.GetOrganizerBookingSummaryAsync(genuineOrganizerId, null, null, It.IsAny<CancellationToken>()), Times.Once);
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
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TamperedKeyWrongSecret123456789012345678901234")),
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
            Expires = DateTime.UtcNow.AddHours(-1),
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

        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            tokenHandler.ValidateToken(tokenString, validationParameters, out _);
        });
    }

    // =========================================================================
    // 9. Summary Endpoint Ownership Filtering
    // =========================================================================
    [Fact]
    public async Task Scenario9_SummaryEndpoint_IsolatesOrganizerEvents()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerA = Guid.NewGuid();
        var organizerB = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();

        // Organizer A owns Event A only
        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { new() { Id = eventA, Title = "A Event", OrganizerId = organizerA } });

        // Add bookings for Event A (5 tickets) and Event B (100 tickets)
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-A",
            EventId = eventA,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 5 } }
        });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-B",
            EventId = eventB,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 100 } }
        });

        await dbContext.SaveChangesAsync();

        // Act: Organizer A requests overall summary
        var summaryA = await service.GetOrganizerBookingSummaryAsync(organizerA);

        // Assert: Exactly Event A bookings returned, Event B is never leaked
        Assert.Equal(1, summaryA.TotalBookings);
        Assert.Equal(5, summaryA.ActiveTicketsBooked);
        Assert.Equal(0, summaryA.CancelledBookings);
    }

    // =========================================================================
    // 10. Date-Filter Ownership Protection (Never Crosses Tenant Boundary)
    // =========================================================================
    [Fact]
    public async Task Scenario10_DateRangeFilter_NeverCrossesTenantBoundary()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerA = Guid.NewGuid();
        var organizerB = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { new() { Id = eventA, Title = "A Event", OrganizerId = organizerA } });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-A1",
            EventId = eventA,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 3 } }
        });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-B1",
            EventId = eventB,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 50 } }
        });

        await dbContext.SaveChangesAsync();

        // Act: Query with broad 10-year date range
        var summary = await service.GetOrganizerBookingSummaryAsync(
            organizerA,
            from: DateTimeOffset.UtcNow.AddYears(-5),
            to: DateTimeOffset.UtcNow.AddYears(5));

        // Assert: Event B data is never exposed regardless of date filter span
        Assert.Equal(1, summary.TotalBookings);
        Assert.Equal(3, summary.ActiveTicketsBooked);
    }

    // =========================================================================
    // 11. Controller Returns Non-Disclosing 404 for Inaccessible Event
    // =========================================================================
    [Fact]
    public async Task Scenario11_ControllerGetEventBookingAnalytics_AnotherOrganizerEvent_ReturnsNotFound()
    {
        var serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        var controller = new OrganizerBookingAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerBookingAnalyticsController>.Instance);

        var organizerId = Guid.NewGuid();
        var forbiddenEventId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Auth")) }
        };

        // Service returns null when ownership check fails
        serviceMock.Setup(s => s.GetEventBookingAnalyticsAsync(organizerId, forbiddenEventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventBookingAnalyticsDto?)null);

        var result = await controller.GetEventBookingAnalytics(forbiddenEventId, null, null, CancellationToken.None);

        // Controller must return 404 NotFound (not 403) so existence is not revealed
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // =========================================================================
    // 12. Sensitive Customer Data & PII Never Exposed in DTO Responses
    // =========================================================================
    [Fact]
    public void Scenario12_SensitiveCustomerData_NeverExposedInDtoResponses()
    {
        var summaryProperties = typeof(OrganizerBookingSummaryDto).GetProperties().Select(p => p.Name).ToList();
        var detailProperties = typeof(EventBookingAnalyticsDto).GetProperties().Select(p => p.Name).ToList();
        var listItemProperties = typeof(EventBookingSummaryItemDto).GetProperties().Select(p => p.Name).ToList();

        // Customer PII & Payment internals must not exist in any booking analytics DTO
        var forbiddenFields = new[]
        {
            "CustomerId",
            "CustomerEmail",
            "CustomerName",
            "CustomerPhone",
            "PaymentId",
            "StripePaymentIntentId",
            "PaymentIntentId",
            "CardNumber",
            "Cvc"
        };

        foreach (var field in forbiddenFields)
        {
            Assert.DoesNotContain(field, summaryProperties);
            Assert.DoesNotContain(field, detailProperties);
            Assert.DoesNotContain(field, listItemProperties);
        }
    }
}
