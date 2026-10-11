using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// Unit tests for ICancellationEligibilityChecker and related boundary conditions (EP-362).
/// </summary>
public class CancellationEligibilityCheckerTests
{
    private class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public TestTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    private readonly DateTimeOffset _referenceEventDate = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    // Cutoff is 48 hours: 2026-10-13 12:00:00 UTC

    [Fact]
    public void EvaluateEligibility_EligibleBooking_ReturnsTrueAndNullReasonCode()
    {
        // Arrange: 72 hours before event (well before 48h cutoff)
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-ELIGIBLE",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Null(result.ReasonCode);
        Assert.Equal("Booking is eligible for cancellation", result.Explanation);
        Assert.NotNull(result.CutoffUtc);
        Assert.NotNull(result.TimeRemaining);
        Assert.True(result.TimeRemaining > TimeSpan.FromHours(48));
    }

    [Fact]
    public void EvaluateEligibility_AlreadyCancelled_ReturnsIneligibleWithAlreadyCancelledCode()
    {
        // Arrange
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-CANCELLED",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.AlreadyCancelled, result.ReasonCode);
        Assert.Equal("Booking is already cancelled", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_EventStarted_ReturnsIneligibleWithEventAlreadyStartedCode()
    {
        // Arrange: Exact event start time
        var testClock = new TestTimeProvider(_referenceEventDate);
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-STARTED",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.EventAlreadyStarted, result.ReasonCode);
        Assert.Equal("Cannot cancel tickets for an event that has already started or concluded", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_BeforeCutoff_ReturnsEligible()
    {
        // Arrange: 48 hours + 1 second before event start (just before cutoff boundary)
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-48).AddSeconds(-1));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-BEFORE-CUTOFF",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Null(result.ReasonCode);
    }

    [Fact]
    public void EvaluateEligibility_ExactlyAtCutoff_ReturnsIneligibleWithCancellationWindowClosedCode()
    {
        // Arrange: Exactly 48 hours before event start (cutoff deadline reached)
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-48));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-EXACT-CUTOFF",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.CancellationWindowClosed, result.ReasonCode);
        Assert.Equal("The cancellation window for this event has closed", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_AfterCutoff_ReturnsIneligibleWithCancellationWindowClosedCode()
    {
        // Arrange: 48 hours - 1 second before event start (past the cutoff deadline)
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-48).AddSeconds(1));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-AFTER-CUTOFF",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.CancellationWindowClosed, result.ReasonCode);
        Assert.Equal("The cancellation window for this event has closed", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_InvalidBookingStatus_ReturnsIneligibleWithInvalidBookingStatusCode()
    {
        // Arrange
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-INVALID-STATUS",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = (BookingStatus)999, // Unrecognized enum value
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.InvalidBookingStatus, result.ReasonCode);
        Assert.Equal("Booking status is not eligible for cancellation", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_InvalidPaymentStatus_ReturnsIneligibleWithPaymentNotEligibleCode()
    {
        // Arrange: Payment failed booking
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-PAYMENT-FAILED",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PaymentFailed,
            TotalAmount = 100m
        };

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.PaymentNotEligible, result.ReasonCode);
        Assert.Equal("Cannot cancel a failed payment booking", result.Explanation);
    }

    [Fact]
    public void EvaluateEligibility_UnauthorizedCustomer_ReturnsIneligibleWithUnauthorizedCode()
    {
        // Arrange
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "EP-2026-WRONG-USER",
            CustomerId = ownerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m
        };

        // Act: Attacker tries to evaluate owner's booking
        var result = checker.EvaluateEligibility(booking, attackerId, _referenceEventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.Unauthorized, result.ReasonCode);
        Assert.Equal("Booking not found or access denied", result.Explanation);
    }

    [Fact]
    public async Task EvaluateEligibilityAsync_BookingNotFound_ReturnsIneligibleWithBookingNotFoundCode()
    {
        // Arrange
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        var nonExistentBookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Act
        var result = await checker.EvaluateEligibilityAsync(nonExistentBookingId, customerId);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(CancellationReasonCodes.BookingNotFound, result.ReasonCode);
        Assert.Equal("Booking not found", result.Explanation);
    }

    [Fact]
    public async Task EvaluateEligibilityAsync_ResolvesEventDateFromEventService_CorrectlyValidatesBoundary()
    {
        // Arrange: 72 hours before event start
        var testClock = new TestTimeProvider(_referenceEventDate.AddHours(-72));
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-ASYNC-EVENT",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 150m
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var clientMock = new Mock<IEventAvailabilityClient>();
        clientMock
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Grand Concert",
                EventDate = _referenceEventDate.UtcDateTime,
                Status = "Published"
            });

        var checker = new CancellationEligibilityChecker(context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, testClock);

        // Act
        var result = await checker.EvaluateEligibilityAsync(bookingId, customerId);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Null(result.ReasonCode);
        clientMock.Verify(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckCancellationEligibility_Endpoint_ReturnsOkWithEligibilityDetails()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-CTRL-ELIGIBLE",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var eligibilityCheckerMock = new Mock<ICancellationEligibilityChecker>();
        eligibilityCheckerMock
            .Setup(c => c.EvaluateEligibilityAsync(bookingId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancellationEligibilityResult.Eligible(DateTimeOffset.UtcNow.AddDays(2), TimeSpan.FromDays(4)));

        var controller = new BookingsController(
            context,
            new Mock<IEventAvailabilityClient>().Object,
            new Mock<IBookingReferenceGenerator>().Object,
            new Mock<IBookingEventPublisher>().Object,
            NullLogger<BookingsController>.Instance,
            new Mock<ICartService>().Object,
            new Mock<IBookingHistoryService>().Object,
            new Mock<IBookingCancellationService>().Object,
            eligibilityCheckerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var actionResult = await controller.CheckCancellationEligibility(bookingId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<CancellationEligibilityResponseDto>(okResult.Value);
        Assert.True(response.IsEligible);
        Assert.Null(response.ReasonCode);
    }

    [Fact]
    public async Task CheckCancellationEligibility_Endpoint_WhenUnauthorizedOrNotFound_Returns404NotFound()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var eligibilityCheckerMock = new Mock<ICancellationEligibilityChecker>();
        eligibilityCheckerMock
            .Setup(c => c.EvaluateEligibilityAsync(bookingId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancellationEligibilityResult.Ineligible(CancellationReasonCodes.Unauthorized, "Booking not found or access denied"));

        var controller = new BookingsController(
            context,
            new Mock<IEventAvailabilityClient>().Object,
            new Mock<IBookingReferenceGenerator>().Object,
            new Mock<IBookingEventPublisher>().Object,
            NullLogger<BookingsController>.Instance,
            new Mock<ICartService>().Object,
            new Mock<IBookingHistoryService>().Object,
            new Mock<IBookingCancellationService>().Object,
            eligibilityCheckerMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var actionResult = await controller.CheckCancellationEligibility(bookingId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }
}
