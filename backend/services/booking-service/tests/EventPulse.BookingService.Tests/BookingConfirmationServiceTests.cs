using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class BookingConfirmationServiceTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    private (BookingConfirmationService service, Mock<ILogger<BookingConfirmationService>> loggerMock) CreateService(BookingDbContext context)
    {
        var loggerMock = new Mock<ILogger<BookingConfirmationService>>();
        var service = new BookingConfirmationService(context, loggerMock.Object);
        return (service, loggerMock);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenPendingPayment_ShouldTransitionToConfirmedAndSetTimestamp()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TEST01",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = 150.0m,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ConfirmedAt = null
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var beforeConfirmationTime = DateTimeOffset.UtcNow;

        // Act
        var result = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        // Assert - Return Result
        Assert.NotNull(result);
        Assert.Equal(BookingConfirmationStatus.Confirmed, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(bookingId, result.BookingId);
        Assert.NotNull(result.Booking);
        Assert.Equal(BookingStatus.Confirmed, result.Booking.Status);
        Assert.NotNull(result.Booking.ConfirmedAt);
        Assert.True(result.Booking.ConfirmedAt >= beforeConfirmationTime);

        // Assert - Persisted Database State
        var persistedBooking = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Confirmed, persistedBooking.Status);
        Assert.NotNull(persistedBooking.ConfirmedAt);
        Assert.True(persistedBooking.ConfirmedAt >= beforeConfirmationTime);

        // Verify Structured Log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Booking {bookingId} confirmed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenAlreadyConfirmed_ShouldBeIdempotentNoOpAndPreserveOriginalTimestamp()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var originalConfirmedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TEST02",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 250.0m,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ConfirmedAt = originalConfirmedAt
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        // Assert - Return Result
        Assert.NotNull(result);
        Assert.Equal(BookingConfirmationStatus.AlreadyConfirmed, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(bookingId, result.BookingId);
        Assert.NotNull(result.Booking);
        Assert.Equal(BookingStatus.Confirmed, result.Booking.Status);
        Assert.Equal(originalConfirmedAt, result.Booking.ConfirmedAt);

        // Assert - Persisted Database State
        var persistedBooking = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Confirmed, persistedBooking.Status);
        Assert.Equal(originalConfirmedAt, persistedBooking.ConfirmedAt);

        // Verify Idempotent Structured Log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already confirmed; treating operation as idempotent")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenBookingNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var nonExistentBookingId = Guid.NewGuid();

        // Act
        var result = await service.ConfirmBookingAfterPaymentAsync(nonExistentBookingId, CancellationToken.None);

        // Assert - Return Result
        Assert.NotNull(result);
        Assert.Equal(BookingConfirmationStatus.NotFound, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(nonExistentBookingId, result.BookingId);
        Assert.Null(result.Booking);

        // Verify Warning Log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found for confirmation")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenCancelled_ShouldRejectConfirmationAndPreserveCancelledStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TEST03",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            TotalAmount = 75.0m,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ConfirmedAt = null
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        // Assert - Return Result
        Assert.NotNull(result);
        Assert.Equal(BookingConfirmationStatus.InvalidState, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(bookingId, result.BookingId);

        // Assert - Persisted Database State Unchanged
        var persistedBooking = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Cancelled, persistedBooking.Status);
        Assert.Null(persistedBooking.ConfirmedAt);

        // Verify Warning Log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot confirm Booking") && v.ToString()!.Contains("Cancelled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenPaymentFailed_ShouldRejectConfirmationAndPreservePaymentFailedStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TEST04",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PaymentFailed,
            TotalAmount = 50.0m,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            ConfirmedAt = null
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        // Assert - Return Result
        Assert.NotNull(result);
        Assert.Equal(BookingConfirmationStatus.InvalidState, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(bookingId, result.BookingId);

        // Assert - Persisted Database State Unchanged
        var persistedBooking = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.PaymentFailed, persistedBooking.Status);
        Assert.Null(persistedBooking.ConfirmedAt);

        // Verify Warning Log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot confirm Booking") && v.ToString()!.Contains("PaymentFailed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmBookingAfterPaymentAsync_WhenCalledTwice_SecondCallIsIdempotentAndCausesNoDuplicateChanges()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-TEST05",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = 300.0m,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ConfirmedAt = null
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act 1: Initial call transitions from PendingPayment to Confirmed
        var result1 = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        Assert.Equal(BookingConfirmationStatus.Confirmed, result1.Status);
        Assert.True(result1.IsSuccess);
        var firstConfirmedAt = result1.Booking!.ConfirmedAt;
        Assert.NotNull(firstConfirmedAt);

        // Act 2: Duplicate delivery (e.g. duplicate Kafka PaymentSucceeded event)
        var result2 = await service.ConfirmBookingAfterPaymentAsync(bookingId, CancellationToken.None);

        // Assert 2: Second call succeeds idempotently without mutating timestamp or status
        Assert.Equal(BookingConfirmationStatus.AlreadyConfirmed, result2.Status);
        Assert.True(result2.IsSuccess);
        Assert.Equal(firstConfirmedAt, result2.Booking!.ConfirmedAt);

        // Verify database state matches first confirmation
        var persistedBooking = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Confirmed, persistedBooking.Status);
        Assert.Equal(firstConfirmedAt, persistedBooking.ConfirmedAt);
    }
}
