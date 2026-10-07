using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Moq;
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

    [Fact]
    public async Task GetCustomerBookingHistoryAsync_ShouldEnrichWithEventDetails_WhenEventClientProvided()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var mockEventClient = new Moq.Mock<IEventAvailabilityClient>();
        mockEventClient
            .Setup(c => c.GetEventSummaryAsync(eventId, Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Rock Concert 2026",
                Venue = "Grand Arena",
                EventDate = new DateTime(2026, 11, 20, 19, 0, 0, DateTimeKind.Utc)
            });

        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance, mockEventClient.Object);

        context.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "REF-EVENT-1",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem>
            {
                new BookingItem { Id = Guid.NewGuid(), TicketTypeId = Guid.NewGuid(), TicketName = "VIP", Quantity = 2, UnitPrice = 100m, Subtotal = 200m }
            }
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetCustomerBookingHistoryAsync(customerId, new BookingHistoryQueryParameters());

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Rock Concert 2026", item.EventName);
        Assert.Equal("Rock Concert 2026", item.EventTitle);
        Assert.Equal(new DateTime(2026, 11, 20, 19, 0, 0, DateTimeKind.Utc), item.EventDate);
    }

    [Fact]
    public async Task GetCustomerBookingHistoryAsync_ShouldReturnEmptyList_WhenNoBookingsExist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);
        var customerId = Guid.NewGuid();

        // Act
        var result = await service.GetCustomerBookingHistoryAsync(customerId, new BookingHistoryQueryParameters());

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetCustomerBookingDetailAsync_ShouldReturnSuccess_WhenBookingOwnedByCaller()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var mockEventClient = new Moq.Mock<IEventAvailabilityClient>();
        mockEventClient
            .Setup(c => c.GetEventSummaryAsync(eventId, Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Jazz Fest",
                Venue = "City Hall",
                EventDate = new DateTime(2026, 12, 1, 18, 30, 0, DateTimeKind.Utc)
            });

        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance, mockEventClient.Object);

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            BookingReference = "REF-DETAIL-1",
            CustomerId = customerId,
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 150m,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfirmedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            Items = new List<BookingItem>
            {
                new BookingItem { Id = Guid.NewGuid(), TicketTypeId = Guid.NewGuid(), TicketName = "General", Quantity = 3, UnitPrice = 50m, Subtotal = 150m }
            },
            Tickets = new List<Ticket>
            {
                new Ticket
                {
                    Id = Guid.NewGuid(),
                    TicketCode = "TCK-001",
                    EventId = eventId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "General",
                    TicketSequence = 1,
                    Status = TicketStatus.Valid,
                    ValidationToken = "vt-001"
                }
            }
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetCustomerBookingDetailAsync(bookingId, customerId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Found);
        Assert.True(result.IsAuthorized);
        Assert.NotNull(result.Booking);
        Assert.Equal(bookingId, result.Booking.Id);
        Assert.Equal("REF-DETAIL-1", result.Booking.BookingReference);
        Assert.Equal("Jazz Fest", result.Booking.EventName);
        Assert.Equal("City Hall", result.Booking.EventVenue);
        Assert.Equal(3, result.Booking.TotalTickets);
        Assert.Single(result.Booking.Items);
        Assert.Single(result.Booking.Tickets);
        Assert.Equal("Jazz Fest", result.Booking.Tickets[0].EventName);
        Assert.Equal("City Hall", result.Booking.Tickets[0].EventVenue);
    }

    [Fact]
    public async Task GetCustomerBookingDetailAsync_ShouldReturnForbidden_WhenNotOwnedAndNotAdmin()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerCustomerId = Guid.NewGuid();
        var callingCustomerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            BookingReference = "REF-SECRET",
            CustomerId = ownerCustomerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetCustomerBookingDetailAsync(bookingId, callingCustomerId, isAdmin: false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Found);
        Assert.False(result.IsAuthorized);
        Assert.Null(result.Booking);
    }

    [Fact]
    public async Task GetCustomerBookingDetailAsync_ShouldReturnSuccess_WhenNotOwned_ButCallerIsAdmin()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerCustomerId = Guid.NewGuid();
        var adminCustomerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            BookingReference = "REF-ADMIN-VIEW",
            CustomerId = ownerCustomerId,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetCustomerBookingDetailAsync(bookingId, adminCustomerId, isAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Found);
        Assert.True(result.IsAuthorized);
        Assert.NotNull(result.Booking);
        Assert.Equal("REF-ADMIN-VIEW", result.Booking.BookingReference);
    }

    [Fact]
    public async Task GetCustomerBookingDetailAsync_ShouldReturnNotFound_WhenBookingDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new BookingHistoryService(context, NullLogger<BookingHistoryService>.Instance);

        // Act
        var result = await service.GetCustomerBookingDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Found);
        Assert.Null(result.Booking);
    }
}
