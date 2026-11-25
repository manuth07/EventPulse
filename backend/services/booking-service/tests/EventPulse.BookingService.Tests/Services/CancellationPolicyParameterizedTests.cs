using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// SE3112 - Unit Testing &amp; Mocking.
/// Parameterized (Theory) tests for the cancellation policy:
///   1) CancellationEligibilityChecker: 48-hour cutoff boundary and status rules (pure logic, fixed clock stub).
///   2) BookingCancellationService: which collaborators are called per booking status (Moq mocks + Verify),
///      with the eligibility checker replaced by a stub so only the service logic is under test.
/// No database server, Kafka, Azure or payment provider is used (EF Core InMemory + mocks only).
/// </summary>
[Trait("Category", "SE3112-Unit")]
public class CancellationPolicyParameterizedTests
{
    // Event starts 2026-10-15 12:00 UTC; the cancellation cutoff is 48h before that.
    private static readonly DateTimeOffset EventDate = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Stub: a clock frozen at a chosen instant, so boundary tests are deterministic.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    private static Booking NewBooking(Guid customerId, BookingStatus status, decimal totalAmount = 100m)
    {
        var bookingId = Guid.NewGuid();
        return new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-SE3112",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = status,
            TotalAmount = totalAmount,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "General Admission",
                    Quantity = 2,
                    UnitPrice = 50m,
                    Subtotal = 100m
                }
            }
        };
    }

    // ---------------------------------------------------------------------
    // 1) Cutoff boundary (valid / invalid / boundary values)
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(604800, true, null)]                                          // 7 days before: eligible
    [InlineData(172801, true, null)]                                          // boundary: 48h + 1s: eligible
    [InlineData(172800, false, CancellationReasonCodes.CancellationWindowClosed)] // boundary: exactly 48h: closed
    [InlineData(3600, false, CancellationReasonCodes.CancellationWindowClosed)]   // 1h before: closed
    [InlineData(0, false, CancellationReasonCodes.EventAlreadyStarted)]       // event starting now: started
    public void EvaluateEligibility_AppliesFortyEightHourCutoff_ForTimeBeforeEvent(
        int secondsBeforeEvent, bool expectedEligible, string? expectedReasonCode)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var clock = new FixedTimeProvider(EventDate.AddSeconds(-secondsBeforeEvent));
        var checker = new CancellationEligibilityChecker(
            context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, clock);
        var customerId = Guid.NewGuid();
        var booking = NewBooking(customerId, BookingStatus.Confirmed);

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, EventDate);

        // Assert
        Assert.Equal(expectedEligible, result.IsEligible);
        Assert.Equal(expectedReasonCode, result.ReasonCode);

        // The pure rule check must not call the remote Event service at all.
        clientMock.VerifyNoOtherCalls();
    }

    // ---------------------------------------------------------------------
    // 2) Booking status rules (including an undefined enum value)
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(BookingStatus.Cancelled, CancellationReasonCodes.AlreadyCancelled)]
    [InlineData(BookingStatus.PaymentFailed, CancellationReasonCodes.PaymentNotEligible)]
    [InlineData((BookingStatus)99, CancellationReasonCodes.InvalidBookingStatus)]
    public void EvaluateEligibility_RejectsNonCancellableStatus_WithExpectedReasonCode(
        BookingStatus status, string expectedReasonCode)
    {
        // Arrange: 72h before the event, so only the status can cause the rejection
        using var context = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var clock = new FixedTimeProvider(EventDate.AddHours(-72));
        var checker = new CancellationEligibilityChecker(
            context, clientMock.Object, NullLogger<CancellationEligibilityChecker>.Instance, clock);
        var customerId = Guid.NewGuid();
        var booking = NewBooking(customerId, status);

        // Act
        var result = checker.EvaluateEligibility(booking, customerId, EventDate);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(expectedReasonCode, result.ReasonCode);
    }

    // ---------------------------------------------------------------------
    // 3) Service logic with mocked collaborators (Setup + Verify)
    // ---------------------------------------------------------------------
    [Theory]
    //          status                      amount  restore release refund
    [InlineData(BookingStatus.Confirmed,      100,   true,   false,  true)]   // paid: restore stock + refund
    [InlineData(BookingStatus.Confirmed,      0,     true,   false,  false)]  // free: restore stock, no refund
    [InlineData(BookingStatus.PendingPayment, 100,   false,  true,   false)]  // unpaid: release hold, no refund
    public async Task CancelBooking_CallsExpectedCollaborators_ForBookingStatus(
        BookingStatus status, int totalAmount, bool expectRestore, bool expectRelease, bool expectRefund)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = NewBooking(customerId, status, totalAmount);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Stub: the eligibility policy is replaced so only the service's own logic is tested
        var checkerStub = new Mock<ICancellationEligibilityChecker>();
        checkerStub
            .Setup(c => c.EvaluateEligibilityAsync(booking.Id, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancellationEligibilityResult.Eligible());

        // Mocks: external collaborators (Event service REST client, Kafka publisher)
        var clientMock = new Mock<IEventAvailabilityClient>();
        var publisherMock = new Mock<IBookingEventPublisher>();

        var service = new BookingCancellationService(
            context,
            publisherMock.Object,
            clientMock.Object,
            NullLogger<BookingCancellationService>.Instance,
            checkerStub.Object,
            new FixedTimeProvider(EventDate.AddHours(-72)));

        // Act
        var result = await service.CancelBookingAsync(booking.Id, customerId, null, CancellationToken.None);

        // Assert: outcome
        Assert.True(result.Success);
        Assert.Equal(status.ToString(), result.PreviousStatus);
        Assert.Equal(BookingStatus.Cancelled.ToString(), result.NewStatus);

        // Assert: interactions
        clientMock.Verify(c => c.RestoreInventoryAsync(
                booking.EventId, booking.Id, It.IsAny<string>(),
                It.IsAny<List<ReserveInventoryItemDto>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            expectRestore ? Times.Once() : Times.Never());

        clientMock.Verify(c => c.ReleaseInventoryAsync(
                booking.EventId, booking.Id, It.IsAny<CancellationToken>()),
            expectRelease ? Times.Once() : Times.Never());

        publisherMock.Verify(p => p.PublishBookingCancelledAsync(
                It.IsAny<BookingCancelledEvent>(), It.IsAny<CancellationToken>()),
            Times.Once());

        publisherMock.Verify(p => p.PublishBookingRefundRequestedAsync(
                It.Is<EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent>(
                    e => e.BookingId == booking.Id && e.RefundAmount == booking.TotalAmount),
                It.IsAny<CancellationToken>()),
            expectRefund ? Times.Once() : Times.Never());
    }

    // ---------------------------------------------------------------------
    // 4) Rejected cancellation: nothing changes, no collaborator is called
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(CancellationReasonCodes.AlreadyCancelled)]
    [InlineData(CancellationReasonCodes.CancellationWindowClosed)]
    public async Task CancelBooking_WhenPolicyRejects_ChangesNothingAndCallsNoCollaborators(string reasonCode)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = NewBooking(customerId, BookingStatus.Confirmed);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var checkerStub = new Mock<ICancellationEligibilityChecker>();
        checkerStub
            .Setup(c => c.EvaluateEligibilityAsync(booking.Id, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancellationEligibilityResult.Ineligible(reasonCode, "Not eligible"));

        var clientMock = new Mock<IEventAvailabilityClient>();
        var publisherMock = new Mock<IBookingEventPublisher>();

        var service = new BookingCancellationService(
            context,
            publisherMock.Object,
            clientMock.Object,
            NullLogger<BookingCancellationService>.Instance,
            checkerStub.Object,
            new FixedTimeProvider(EventDate.AddHours(-72)));

        // Act
        var result = await service.CancelBookingAsync(booking.Id, customerId, null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(reasonCode, result.ReasonCode);

        var stored = await context.Bookings.AsNoTracking().FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);

        // No inventory call and no event published when the policy says no
        clientMock.VerifyNoOtherCalls();
        publisherMock.VerifyNoOtherCalls();
    }
}