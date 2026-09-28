using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class BookingPaymentFailureServiceTests
{
    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static (BookingPaymentFailureService service, Mock<ILogger<BookingPaymentFailureService>> loggerMock) CreateService(BookingDbContext context)
    {
        var loggerMock = new Mock<ILogger<BookingPaymentFailureService>>();
        var service = new BookingPaymentFailureService(context, loggerMock.Object);
        return (service, loggerMock);
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_WhenPendingPayment_TransitionsToPaymentFailed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-01",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            TotalAmount = 100m,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.HandlePaymentFailureAsync(bookingId, "card_declined", "Insufficient funds", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingPaymentFailureStatus.Failed, result.Status);
        Assert.Equal(bookingId, result.BookingId);

        var persisted = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.PaymentFailed, persisted.Status);
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_WhenAlreadyPaymentFailed_IsIdempotentAndRemainsPaymentFailed()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-02",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.PaymentFailed,
            TotalAmount = 100m,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.HandlePaymentFailureAsync(bookingId, "card_declined", "Declined again", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingPaymentFailureStatus.AlreadyFailed, result.Status);
        Assert.Equal(bookingId, result.BookingId);

        var persisted = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.PaymentFailed, persisted.Status);
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_WhenConfirmed_PreservesConfirmedStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-03",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100m,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.HandlePaymentFailureAsync(bookingId, "late_fail", "Late decline", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingPaymentFailureStatus.AlreadyConfirmed, result.Status);
        Assert.Equal(bookingId, result.BookingId);

        var persisted = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.Confirmed, persisted.Status);
        Assert.NotNull(persisted.ConfirmedAt);

        // Verify warning log for stale failure event
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already Confirmed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_WhenCancelled_PreservesCancelledStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-04",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            TotalAmount = 100m,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.HandlePaymentFailureAsync(bookingId, "expired", "Booking cancelled", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingPaymentFailureStatus.Cancelled, result.Status);
        Assert.Equal(bookingId, result.BookingId);

        var persisted = await context.Bookings.FindAsync(bookingId);
        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.Cancelled, persisted.Status);
    }

    [Fact]
    public async Task HandlePaymentFailureAsync_WhenBookingNotFound_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _) = CreateService(context);

        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await service.HandlePaymentFailureAsync(nonExistentId, "error", "No booking", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingPaymentFailureStatus.NotFound, result.Status);
        Assert.Equal(nonExistentId, result.BookingId);
    }
}
