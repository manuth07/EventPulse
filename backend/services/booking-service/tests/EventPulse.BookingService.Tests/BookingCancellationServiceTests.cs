using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class BookingCancellationServiceTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    [Fact]
    public async Task EvaluateCancellationEligibility_WhenBookingDoesNotExist_ReturnsNotEligible()
    {
        using var dbContext = CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<BookingCancellationService>>();
        var service = new BookingCancellationService(dbContext, loggerMock.Object);

        var (isEligible, reason, booking) = await service.EvaluateCancellationEligibilityAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(isEligible);
        Assert.Equal("Booking not found or access denied", reason);
        Assert.Null(booking);
    }

    [Fact]
    public async Task EvaluateCancellationEligibility_WhenCustomerMismatch_ReturnsNotEligible()
    {
        using var dbContext = CreateInMemoryDbContext();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "REF-123",
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<BookingCancellationService>>();
        var service = new BookingCancellationService(dbContext, loggerMock.Object);

        var (isEligible, reason, returnedBooking) = await service.EvaluateCancellationEligibilityAsync(booking.Id, Guid.NewGuid());

        Assert.False(isEligible);
        Assert.Equal("Booking not found or access denied", reason);
        Assert.Null(returnedBooking);
    }

    [Fact]
    public async Task EvaluateCancellationEligibility_WhenAlreadyCancelled_ReturnsNotEligible()
    {
        using var dbContext = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "REF-123",
            CustomerId = customerId,
            Status = BookingStatus.Cancelled
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<BookingCancellationService>>();
        var service = new BookingCancellationService(dbContext, loggerMock.Object);

        var (isEligible, reason, returnedBooking) = await service.EvaluateCancellationEligibilityAsync(booking.Id, customerId);

        Assert.False(isEligible);
        Assert.Equal("Booking is already cancelled", reason);
        Assert.NotNull(returnedBooking);
    }

    [Fact]
    public async Task EvaluateCancellationEligibility_WhenPaymentFailed_ReturnsNotEligible()
    {
        using var dbContext = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "REF-123",
            CustomerId = customerId,
            Status = BookingStatus.PaymentFailed
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<BookingCancellationService>>();
        var service = new BookingCancellationService(dbContext, loggerMock.Object);

        var (isEligible, reason, returnedBooking) = await service.EvaluateCancellationEligibilityAsync(booking.Id, customerId);

        Assert.False(isEligible);
        Assert.Equal("Cannot cancel a failed payment booking", reason);
        Assert.NotNull(returnedBooking);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenEligible_CancelsBookingAndTickets()
    {
        using var dbContext = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "REF-CANCEL-1",
            CustomerId = customerId,
            Status = BookingStatus.Confirmed,
            Tickets = new List<Ticket>
            {
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid },
                new Ticket { Id = Guid.NewGuid(), Status = TicketStatus.Valid }
            }
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<BookingCancellationService>>();
        var service = new BookingCancellationService(dbContext, loggerMock.Object);

        var result = await service.CancelBookingAsync(booking.Id, customerId, new CancelBookingRequest { Reason = "User change of plans" });

        Assert.True(result.Success);
        Assert.Equal("Confirmed", result.PreviousStatus);
        Assert.Equal("Cancelled", result.NewStatus);
        Assert.Equal(booking.BookingReference, result.BookingReference);

        var updated = await dbContext.Bookings.Include(b => b.Tickets).FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
        Assert.All(updated.Tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));
    }
}
