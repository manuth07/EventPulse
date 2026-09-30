using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class BookingHistoryServiceTests
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
    public async Task GetCustomerBookingHistoryAsync_ShouldOnlyReturnBookingsForRequestedCustomer()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-A1",
                CustomerId = customerA,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                Items = new List<BookingItem>
                {
                    new BookingItem { Id = Guid.NewGuid(), TicketTypeId = Guid.NewGuid(), TicketName = "GA", Quantity = 2, UnitPrice = 50m, Subtotal = 100m }
                }
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-A2",
                CustomerId = customerA,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Items = new List<BookingItem>()
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-B1",
                CustomerId = customerB,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-8),
                Items = new List<BookingItem>()
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-B2",
                CustomerId = customerB,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Cancelled,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                Items = new List<BookingItem>()
            }
        };

        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        var queryParams = new BookingHistoryQueryParameters();

        // Act
        var result = await service.GetCustomerBookingHistoryAsync(customerA, queryParams);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
        {
            Assert.Contains(item.BookingReference, new[] { "REF-A1", "REF-A2" });
        });
    }

    [Fact]
    public async Task GetCustomerBookingHistoryAsync_ShouldFilterByStatus()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        var customerId = Guid.NewGuid();

        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-CONFIRMED",
                CustomerId = customerId,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                Items = new List<BookingItem>()
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = "REF-PENDING",
                CustomerId = customerId,
                EventId = Guid.NewGuid(),
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                Items = new List<BookingItem>()
            }
        };

        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        var queryParams = new BookingHistoryQueryParameters
        {
            Status = "Confirmed"
        };

        // Act
        var result = await service.GetCustomerBookingHistoryAsync(customerId, queryParams);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Confirmed", result.Items[0].Status);
        Assert.Equal("REF-CONFIRMED", result.Items[0].BookingReference);
    }

    [Fact]
    public async Task GetCustomerBookingHistoryAsync_ShouldPaginateCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        var customerId = Guid.NewGuid();

        var bookings = Enumerable.Range(1, 5).Select(i => new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = $"REF-{i}",
            CustomerId = customerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i),
            Items = new List<BookingItem>()
        }).ToList();

        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        // Act - Page 1
        var page1Params = new BookingHistoryQueryParameters
        {
            Page = 1,
            PageSize = 2
        };
        var page1Result = await service.GetCustomerBookingHistoryAsync(customerId, page1Params);

        // Assert - Page 1
        Assert.NotNull(page1Result);
        Assert.Equal(2, page1Result.Items.Count);
        Assert.Equal(5, page1Result.TotalCount);
        Assert.Equal(3, page1Result.TotalPages);
        Assert.True(page1Result.HasNextPage);
        Assert.False(page1Result.HasPreviousPage);

        // Act - Page 3
        var page3Params = new BookingHistoryQueryParameters
        {
            Page = 3,
            PageSize = 2
        };
        var page3Result = await service.GetCustomerBookingHistoryAsync(customerId, page3Params);

        // Assert - Page 3
        Assert.NotNull(page3Result);
        Assert.Single(page3Result.Items);
        Assert.False(page3Result.HasNextPage);
        Assert.True(page3Result.HasPreviousPage);
    }
}
