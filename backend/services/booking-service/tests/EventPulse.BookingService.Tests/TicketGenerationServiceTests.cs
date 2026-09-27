using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class TicketGenerationServiceTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    private (TicketGenerationService service, Mock<ITicketCodeGenerator> codeGenMock, Mock<IValidationTokenGenerator> tokenGenMock, Mock<ILogger<TicketGenerationService>> loggerMock)
        CreateService(BookingDbContext context, bool useRealGenerators = false)
    {
        var loggerMock = new Mock<ILogger<TicketGenerationService>>();

        if (useRealGenerators)
        {
            var codeGen = new TicketCodeGenerator(context);
            var tokenGen = new ValidationTokenGenerator(context);
            var realService = new TicketGenerationService(context, codeGen, tokenGen, loggerMock.Object);
            return (realService, new Mock<ITicketCodeGenerator>(), new Mock<IValidationTokenGenerator>(), loggerMock);
        }

        var mockCodeGen = new Mock<ITicketCodeGenerator>();
        var mockTokenGen = new Mock<IValidationTokenGenerator>();

        var codeCounter = 1;
        mockCodeGen.Setup(x => x.GenerateUniqueTicketCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"EP-TKT-2026-TEST{codeCounter++:D4}");

        var tokenCounter = 1;
        mockTokenGen.Setup(x => x.GenerateUniqueValidationTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"test-validation-token-{tokenCounter++:D4}");

        var service = new TicketGenerationService(context, mockCodeGen.Object, mockTokenGen.Object, loggerMock.Object);
        return (service, mockCodeGen, mockTokenGen, loggerMock);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WhenConfirmedBookingWithQuantityOne_ShouldGenerateExactlyOneTicket()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var bookingItemId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-QTY1",
            CustomerId = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 50.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = bookingItemId,
                    BookingId = bookingId,
                    TicketTypeId = ticketTypeId,
                    TicketName = "General Admission",
                    Quantity = 1,
                    UnitPrice = 50.0m,
                    Subtotal = 50.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketGenerationStatus.Success, result.Status);
        Assert.Equal(1, result.GeneratedCount);
        Assert.Equal(1, result.TotalTicketCount);
        Assert.Single(result.Tickets);

        var ticket = result.Tickets[0];
        Assert.Equal(bookingId, ticket.BookingId);
        Assert.Equal(bookingItemId, ticket.BookingItemId);
        Assert.Equal(ticketTypeId, ticket.TicketTypeId);
        Assert.Equal("General Admission", ticket.TicketName);
        Assert.Equal(1, ticket.TicketSequence);
        Assert.Equal(eventId, ticket.EventId);
        Assert.Equal(TicketStatus.Valid, ticket.Status);
        Assert.False(string.IsNullOrWhiteSpace(ticket.TicketCode));
        Assert.False(string.IsNullOrWhiteSpace(ticket.ValidationToken));

        // Assert Database Persistence
        var persistedTickets = await context.Tickets.Where(t => t.BookingId == bookingId).ToListAsync();
        Assert.Single(persistedTickets);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WhenConfirmedBookingWithQuantityThree_ShouldGenerateExactlyThreeTickets()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var bookingItemId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-QTY3",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 150.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = bookingItemId,
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "VIP Pass",
                    Quantity = 3,
                    UnitPrice = 50.0m,
                    Subtotal = 150.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketGenerationStatus.Success, result.Status);
        Assert.Equal(3, result.GeneratedCount);
        Assert.Equal(3, result.TotalTicketCount);
        Assert.Equal(3, result.Tickets.Count);

        // Verify sequence 1, 2, 3
        var sequences = result.Tickets.Select(t => t.TicketSequence).OrderBy(s => s).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, sequences);

        // Assert Database Persistence
        var persistedCount = await context.Tickets.CountAsync(t => t.BookingId == bookingId);
        Assert.Equal(3, persistedCount);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WithMultipleBookingItems_ShouldGenerateTotalCombinedQuantity()
    {
        // Arrange: VIP x 2, Regular x 1 => 3 tickets
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var vipItemId = Guid.NewGuid();
        var regularItemId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-MULTI",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 250.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = vipItemId,
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "VIP",
                    Quantity = 2,
                    UnitPrice = 100.0m,
                    Subtotal = 200.0m
                },
                new BookingItem
                {
                    Id = regularItemId,
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "Regular",
                    Quantity = 1,
                    UnitPrice = 50.0m,
                    Subtotal = 50.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.GeneratedCount);
        Assert.Equal(3, result.TotalTicketCount);

        var vipTickets = result.Tickets.Where(t => t.BookingItemId == vipItemId).ToList();
        var regularTickets = result.Tickets.Where(t => t.BookingItemId == regularItemId).ToList();

        Assert.Equal(2, vipTickets.Count);
        Assert.Single(regularTickets);
        Assert.Equal(new[] { 1, 2 }, vipTickets.Select(t => t.TicketSequence).OrderBy(s => s));
        Assert.Equal(1, regularTickets[0].TicketSequence);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WhenCalledTwice_SecondCallIsIdempotentAndProducesZeroDuplicates()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _, _, loggerMock) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-IDEM",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 100.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "Standard",
                    Quantity = 2,
                    UnitPrice = 50.0m,
                    Subtotal = 100.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act 1: Initial call
        var result1 = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);
        Assert.Equal(TicketGenerationStatus.Success, result1.Status);
        Assert.Equal(2, result1.GeneratedCount);
        Assert.Equal(2, result1.TotalTicketCount);

        // Act 2: Duplicate call (simulating Kafka redelivery)
        var result2 = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert 2: Idempotent no-op
        Assert.Equal(TicketGenerationStatus.AlreadyGenerated, result2.Status);
        Assert.Equal(0, result2.GeneratedCount);
        Assert.Equal(2, result2.TotalTicketCount);

        // Assert Database State: Still exactly 2 tickets, zero duplicates
        var persistedTickets = await context.Tickets.Where(t => t.BookingId == bookingId).ToListAsync();
        Assert.Equal(2, persistedTickets.Count);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WithPartialExistingTickets_ShouldGenerateOnlyMissingUnits()
    {
        // Arrange: Purchased 3 tickets, but unit #1 already exists (e.g. earlier crash)
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var bookingItemId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-PARTIAL",
            CustomerId = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            TotalAmount = 150.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = bookingItemId,
                    BookingId = bookingId,
                    TicketTypeId = ticketTypeId,
                    TicketName = "Festival Pass",
                    Quantity = 3,
                    UnitPrice = 50.0m,
                    Subtotal = 150.0m
                }
            }
        };

        var preExistingTicket = new Ticket
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingItemId = bookingItemId,
            TicketTypeId = ticketTypeId,
            TicketName = "Festival Pass",
            TicketSequence = 1, // Unit 1 already exists
            EventId = eventId,
            TicketCode = "EP-TKT-EXISTING-01",
            ValidationToken = "existing-token-01",
            Status = TicketStatus.Valid,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        context.Bookings.Add(booking);
        context.Tickets.Add(preExistingTicket);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert: Generated only the 2 missing units
        Assert.Equal(TicketGenerationStatus.Success, result.Status);
        Assert.Equal(2, result.GeneratedCount);
        Assert.Equal(3, result.TotalTicketCount);

        // Verify database has sequences 1, 2, 3
        var allPersisted = await context.Tickets
            .Where(t => t.BookingId == bookingId)
            .OrderBy(t => t.TicketSequence)
            .ToListAsync();

        Assert.Equal(3, allPersisted.Count);
        Assert.Equal(new[] { 1, 2, 3 }, allPersisted.Select(t => t.TicketSequence));
        Assert.Equal("EP-TKT-EXISTING-01", allPersisted[0].TicketCode); // Preserved original
    }

    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.PaymentFailed)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task GenerateTicketsForBookingAsync_WhenBookingNotConfirmed_ShouldRejectTicketGeneration(BookingStatus invalidStatus)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-INVALID",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = invalidStatus,
            TotalAmount = 50.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "General",
                    Quantity = 2,
                    UnitPrice = 25.0m,
                    Subtotal = 50.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(TicketGenerationStatus.InvalidStatus, result.Status);
        Assert.Equal(0, result.GeneratedCount);
        Assert.Empty(result.Tickets);

        // Assert: Zero tickets in database
        var ticketCount = await context.Tickets.CountAsync(t => t.BookingId == bookingId);
        Assert.Equal(0, ticketCount);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_WhenBookingNotFound_ShouldReturnNotFoundResult()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context);

        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(nonExistentId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(TicketGenerationStatus.NotFound, result.Status);
        Assert.Equal(0, result.GeneratedCount);
        Assert.Empty(result.Tickets);
    }

    [Fact]
    public async Task GenerateTicketsForBookingAsync_UsingRealGenerators_ShouldProduceUniqueCodesAndValidationTokens()
    {
        // Arrange: Test with actual TicketCodeGenerator and ValidationTokenGenerator implementations
        using var context = CreateInMemoryDbContext();
        var (service, _, _, _) = CreateService(context, useRealGenerators: true);

        var bookingId = Guid.NewGuid();
        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-2026-REALGEN",
            CustomerId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            TotalAmount = 250.0m,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TicketTypeId = Guid.NewGuid(),
                    TicketName = "Conference Pass",
                    Quantity = 5,
                    UnitPrice = 50.0m,
                    Subtotal = 250.0m
                }
            }
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GenerateTicketsForBookingAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.GeneratedCount);
        Assert.Equal(5, result.Tickets.Count);

        // Verify each ticket has unique Id
        var ids = result.Tickets.Select(t => t.Id).Distinct().ToList();
        Assert.Equal(5, ids.Count);

        // Verify each ticket has unique TicketCode starting with EP-TKT-
        var codes = result.Tickets.Select(t => t.TicketCode).Distinct().ToList();
        Assert.Equal(5, codes.Count);
        Assert.All(codes, code => Assert.StartsWith("EP-TKT-", code));

        // Verify each ticket has unique 64-char hex ValidationToken
        var tokens = result.Tickets.Select(t => t.ValidationToken).Distinct().ToList();
        Assert.Equal(5, tokens.Count);
        Assert.All(tokens, token =>
        {
            Assert.Equal(64, token.Length);
            Assert.Matches("^[0-9a-f]{64}$", token);
        });
    }
}
