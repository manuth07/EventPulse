using System.Security.Claims;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class TicketTypeRemainingQuantityTests
{
    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    private static ClaimsPrincipal CreateOrganizerUser(Guid organizerId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    // =========================================================================
    // 1. DOMAIN MODEL & BOUNDARY TESTS (TicketType entity)
    // =========================================================================

    [Fact]
    public void TicketType_InitialRemainingQuantity_EqualsTotalQuantity()
    {
        // Arrange
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "VIP",
            Price = 5000m,
            Capacity = 100,
            BookedQuantity = 0,
        };

        // Assert
        Assert.Equal(100, ticketType.TotalQuantity);
        Assert.Equal(0, ticketType.SoldQuantity);
        Assert.Equal(100, ticketType.RemainingQuantity);
    }

    [Fact]
    public void TicketType_RemainingQuantity_AccuratelyReflectsSoldTickets()
    {
        // Arrange
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "General Admission",
            Price = 2500m,
            Capacity = 200,
            BookedQuantity = 45,
        };

        // Assert
        Assert.Equal(200, ticketType.TotalQuantity);
        Assert.Equal(45, ticketType.SoldQuantity);
        Assert.Equal(155, ticketType.RemainingQuantity);
    }

    [Fact]
    public void TicketType_RemainingQuantity_IsZero_WhenCompletelySoldOut()
    {
        // Arrange
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "Early Bird",
            Price = 1500m,
            Capacity = 50,
            BookedQuantity = 50,
        };

        // Assert
        Assert.Equal(50, ticketType.TotalQuantity);
        Assert.Equal(50, ticketType.SoldQuantity);
        Assert.Equal(0, ticketType.RemainingQuantity);
    }

    [Fact]
    public void TicketType_RemainingQuantity_NeverNegative_WhenOverbooked()
    {
        // Boundary case: sold tickets exceed capacity (e.g. race condition/anomaly)
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "Standing",
            Price = 1000m,
            Capacity = 50,
            BookedQuantity = 55,
        };

        // Assert: remaining quantity must be clamped to 0, never negative
        Assert.Equal(0, ticketType.RemainingQuantity);
    }

    [Fact]
    public void TicketType_RemainingQuantity_NeverExceedsTotalQuantity_WhenSoldQuantityNegative()
    {
        // Boundary case: negative booked tickets anomaly
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            Name = "Balcony",
            Price = 3000m,
            Capacity = 80,
            BookedQuantity = -5,
        };

        // Assert: remaining quantity must be clamped to TotalQuantity, never exceed it
        Assert.Equal(80, ticketType.RemainingQuantity);
    }

    // =========================================================================
    // 2. DTO MAPPING TESTS (TicketTypeDto & PublicTicketTypeDto)
    // =========================================================================

    [Fact]
    public void TicketTypeDto_ExposesTotalSoldAndRemainingQuantityAccurately()
    {
        var dto = new TicketTypeDto
        {
            Id = Guid.NewGuid(),
            Name = "Gold Pass",
            Price = 10000m,
            Capacity = 100,
            BookedQuantity = 30,
        };

        Assert.Equal(100, dto.TotalQuantity);
        Assert.Equal(30, dto.SoldQuantity);
        Assert.Equal(70, dto.RemainingQuantity);
        Assert.Equal(70, dto.AvailableQuantity);
    }

    [Fact]
    public void PublicTicketTypeDto_ExposesTotalAndRemainingQuantity()
    {
        var dto = new PublicTicketTypeDto
        {
            Id = Guid.NewGuid(),
            Name = "Standard",
            Price = 2000m,
            TotalQuantity = 150,
            RemainingQuantity = 25,
            AvailableQuantity = 25,
            IsSoldOut = false,
        };

        Assert.Equal(150, dto.TotalQuantity);
        Assert.Equal(25, dto.RemainingQuantity);
        Assert.Equal(25, dto.AvailableQuantity);
        Assert.False(dto.IsSoldOut);
    }

    // =========================================================================
    // 3. SERVICE TESTS (TicketTypeService CreateAsync)
    // =========================================================================

    [Fact]
    public async Task CreateAsync_NewTicketType_BeginsWithRemainingQuantityEqualToTotalQuantity()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Tech Conference 2026",
            Description = "Annual tech conference",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var request = new CreateTicketTypeRequest
        {
            Name = "Early Bird",
            Price = 5000m,
            Capacity = 75,
        };

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await service.CreateAsync(liveEvent.Id, request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);
        Assert.NotNull(result);
        Assert.Equal(75, result.Capacity);
        Assert.Equal(75, result.TotalQuantity);
        Assert.Equal(0, result.BookedQuantity);
        Assert.Equal(0, result.SoldQuantity);
        Assert.Equal(75, result.RemainingQuantity);
        Assert.Equal(75, result.AvailableQuantity);
    }

    [Fact]
    public async Task CreateAsync_RejectsTotalQuantity_LessThanOne()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Music Fest",
            Description = "Music Festival",
            Venue = "Viharamahadevi Park",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Approved,
        };
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);

        // Act - zero capacity
        var (resultZero, errorZero, _, _, _) =
            await service.CreateAsync(liveEvent.Id, new CreateTicketTypeRequest { Name = "VIP", Price = 1000m, Capacity = 0 }, organizerId);

        // Act - negative capacity
        var (resultNeg, errorNeg, _, _, _) =
            await service.CreateAsync(liveEvent.Id, new CreateTicketTypeRequest { Name = "VIP", Price = 1000m, Capacity = -10 }, organizerId);

        // Assert
        Assert.Null(resultZero);
        Assert.Equal("Total quantity must be at least 1.", errorZero);

        Assert.Null(resultNeg);
        Assert.Equal("Total quantity must be at least 1.", errorNeg);
    }

    [Fact]
    public async Task CreateAsync_AcceptsTotalQuantityAlias_InRequest()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Art Exhibition",
            Description = "Art exhibition showcase",
            Venue = "Lionel Wendt",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var request = new CreateTicketTypeRequest
        {
            Name = "Standard Entry",
            Price = 1200m,
            TotalQuantity = 120, // using TotalQuantity alias
        };

        // Act
        var (result, error, _, _, _) = await service.CreateAsync(liveEvent.Id, request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(120, result.TotalQuantity);
        Assert.Equal(120, result.Capacity);
        Assert.Equal(120, result.RemainingQuantity);
    }

    // =========================================================================
    // 4. SERVICE TESTS (TicketTypeService UpdateAsync)
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_IncreasingTotalQuantity_UpdatesRemainingQuantity()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Gala Evening",
            Description = "Charity gala",
            Venue = "Cinnamon Grand",
            EventDate = DateTime.UtcNow.AddMonths(2),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Table for 4",
            Price = 20000m,
            Capacity = 50,
            BookedQuantity = 20, // 20 sold -> 30 remaining
        };
        context.TicketTypes.Add(ticketType);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var updateRequest = new UpdateTicketTypeRequest
        {
            Name = "Table for 4",
            Price = 20000m,
            Capacity = 80, // Expand total to 80 -> remaining should become 60
        };

        // Act
        var (result, error, isNotFound, isForbidden) =
            await service.UpdateAsync(liveEvent.Id, ticketType.Id, updateRequest, organizerId);

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.NotNull(result);
        Assert.Equal(80, result.TotalQuantity);
        Assert.Equal(20, result.SoldQuantity);
        Assert.Equal(60, result.RemainingQuantity);
        Assert.Equal(60, result.AvailableQuantity);
    }

    [Fact]
    public async Task UpdateAsync_ReducingTotalQuantityDownToSoldQuantity_SetsRemainingToZero()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Gala Evening",
            Description = "Charity gala",
            Venue = "Cinnamon Grand",
            EventDate = DateTime.UtcNow.AddMonths(2),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "VIP Table",
            Price = 20000m,
            Capacity = 50,
            BookedQuantity = 30, // 30 sold
        };
        context.TicketTypes.Add(ticketType);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var updateRequest = new UpdateTicketTypeRequest
        {
            Name = "VIP Table",
            Price = 20000m,
            Capacity = 30, // Reduce total quantity exactly to sold quantity
        };

        // Act
        var (result, error, _, _) =
            await service.UpdateAsync(liveEvent.Id, ticketType.Id, updateRequest, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(30, result.TotalQuantity);
        Assert.Equal(30, result.SoldQuantity);
        Assert.Equal(0, result.RemainingQuantity);
        Assert.Equal(0, result.AvailableQuantity);
    }

    [Fact]
    public async Task UpdateAsync_RejectsReducingTotalQuantity_BelowSoldQuantity()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Rock Concert",
            Description = "Live rock concert",
            Venue = "Sugathadasa Stadium",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "General",
            Price = 3000m,
            Capacity = 100,
            BookedQuantity = 40, // 40 sold
        };
        context.TicketTypes.Add(ticketType);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var updateRequest = new UpdateTicketTypeRequest
        {
            Name = "General",
            Price = 3000m,
            Capacity = 35, // Cannot reduce below 40 sold
        };

        // Act
        var (result, error, isNotFound, isForbidden) =
            await service.UpdateAsync(liveEvent.Id, ticketType.Id, updateRequest, organizerId);

        // Assert
        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Contains("cannot be reduced below", error);
        Assert.Contains("40", error);
    }

    [Fact]
    public async Task UpdateAsync_RejectsTotalQuantity_LessThanOne()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Workshop",
            Description = "Hands-on workshop",
            Venue = "SLIDA",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Participant",
            Price = 2000m,
            Capacity = 30,
            BookedQuantity = 0,
        };
        context.TicketTypes.Add(ticketType);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);
        var updateRequest = new UpdateTicketTypeRequest
        {
            Name = "Participant",
            Price = 2000m,
            Capacity = 0,
        };

        // Act
        var (result, error, _, _) =
            await service.UpdateAsync(liveEvent.Id, ticketType.Id, updateRequest, organizerId);

        // Assert
        Assert.Null(result);
        Assert.Equal("Total quantity must be at least 1.", error);
    }

    // =========================================================================
    // 5. PUBLIC AVAILABILITY TESTS (TicketTypeService GetPublicByEventIdAsync)
    // =========================================================================

    [Fact]
    public async Task GetPublicByEventIdAsync_ReturnsAccurateRemainingAndTotalQuantity()
    {
        // Arrange
        using var context = CreateContext();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = Guid.NewGuid(),
            Title = "Public Showcase",
            Description = "Open public event",
            Venue = "Nelum Pokuna",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket1 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "VIP",
            Price = 7500m,
            Capacity = 50,
            BookedQuantity = 10,
        };
        var ticket2 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "General",
            Price = 2500m,
            Capacity = 100,
            BookedQuantity = 100, // Sold out
        };
        context.TicketTypes.AddRange(ticket1, ticket2);
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);

        // Act
        var (result, isNotFound) = await service.GetPublicByEventIdAsync(liveEvent.Id);

        // Assert
        Assert.False(isNotFound);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var vip = result.First(t => t.Name == "VIP");
        Assert.Equal(50, vip.TotalQuantity);
        Assert.Equal(40, vip.RemainingQuantity);
        Assert.Equal(40, vip.AvailableQuantity);
        Assert.False(vip.IsSoldOut);

        var general = result.First(t => t.Name == "General");
        Assert.Equal(100, general.TotalQuantity);
        Assert.Equal(0, general.RemainingQuantity);
        Assert.Equal(0, general.AvailableQuantity);
        Assert.True(general.IsSoldOut);
    }

    [Fact]
    public async Task GetPublicByEventIdAsync_WhenCancellationPending_MaintainsRemainingQuantityButBlocksAvailable()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Cancelling Event",
            Description = "Event under cancellation review",
            Venue = "Town Hall",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Admission",
            Price = 3000m,
            Capacity = 100,
            BookedQuantity = 20,
        };
        context.TicketTypes.Add(ticket);

        context.EventCancellationRequests.Add(new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Venue unavailable",
            Status = EventCancellationRequestStatus.Pending,
        });
        await context.SaveChangesAsync();

        var service = new TicketTypeService(context);

        // Act
        var (result, isNotFound) = await service.GetPublicByEventIdAsync(liveEvent.Id);

        // Assert
        Assert.False(isNotFound);
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(100, item.TotalQuantity);
        Assert.Equal(80, item.RemainingQuantity); // Unsold tickets
        Assert.Equal(0, item.AvailableQuantity); // Blocked for sale
        Assert.True(item.IsSoldOut);
    }

    // =========================================================================
    // 6. CONTROLLER INTEGRATION & VALIDATION TESTS (TicketTypesController)
    // =========================================================================

    [Fact]
    public async Task Controller_CreateTicketType_ReturnsBadRequest_WhenModelStateInvalid()
    {
        // Arrange
        var controller = new TicketTypesController();
        controller.ModelState.AddModelError("Capacity", "Total quantity must be at least 1.");

        var request = new CreateTicketTypeRequest { Name = "VIP", Price = 1000m, Capacity = 0 };

        // Act
        var result = await controller.CreateTicketType(Guid.NewGuid().ToString(), request, default);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Controller_UpdateTicketType_ReturnsBadRequest_WhenCapacityReducedBelowSold()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var serviceMock = new Mock<ITicketTypeService>();
        serviceMock.Setup(s => s.UpdateAsync(eventId, ticketTypeId, It.IsAny<UpdateTicketTypeRequest>(), organizerId, default))
            .ReturnsAsync((null, "Total quantity cannot be reduced below the quantity already sold (35).", false, false));

        var controller = new TicketTypesController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateOrganizerUser(organizerId) }
            }
        };

        var request = new UpdateTicketTypeRequest { Name = "VIP", Price = 5000m, Capacity = 20 };

        // Act
        var result = await controller.UpdateTicketType(eventId.ToString(), ticketTypeId.ToString(), request, default);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Controller_CreateTicketType_ReturnsCreated_WithRemainingQuantity()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var expectedDto = new TicketTypeDto
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "Early Bird",
            Price = 2500m,
            Capacity = 100,
            BookedQuantity = 0,
        };

        var serviceMock = new Mock<ITicketTypeService>();
        serviceMock.Setup(s => s.CreateAsync(eventId, It.IsAny<CreateTicketTypeRequest>(), organizerId, default))
            .ReturnsAsync((expectedDto, null, false, false, false));

        var controller = new TicketTypesController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateOrganizerUser(organizerId) }
            }
        };

        var request = new CreateTicketTypeRequest { Name = "Early Bird", Price = 2500m, Capacity = 100 };

        // Act
        var result = await controller.CreateTicketType(eventId.ToString(), request, default);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var createdDto = Assert.IsType<TicketTypeDto>(createdResult.Value);
        Assert.Equal(100, createdDto.TotalQuantity);
        Assert.Equal(100, createdDto.RemainingQuantity);
        Assert.Equal(0, createdDto.SoldQuantity);
    }
}
