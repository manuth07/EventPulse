using System.Security.Claims;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventPulse.EventService.Tests;

public class TicketTypeRemainingQuantityApiTests
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

    private static TicketTypesController CreateController(EventDbContext context, Guid organizerId)
    {
        var service = new TicketTypeService(context);
        var controller = new TicketTypesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateOrganizerUser(organizerId) }
            }
        };
        return controller;
    }

    private static TicketTypesController CreatePublicController(EventDbContext context)
    {
        var service = new TicketTypeService(context);
        var controller = new TicketTypesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    // =========================================================================
    // 1. ORGANIZER APIS: CREATION & RETRIEVAL (New Ticket Type)
    // =========================================================================

    [Fact]
    public async Task CreateTicketType_ReturnsCorrectRemainingQuantity_ForNewTicketType()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Colombo Jazz Festival",
            Description = "Live music performance",
            Venue = "Galle Face Hotel",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var controller = CreateController(context, organizerId);
        var request = new CreateTicketTypeRequest
        {
            Name = "VIP Lounge",
            Price = 15000m,
            Capacity = 60,
        };

        // Act
        var actionResult = await controller.CreateTicketType(liveEvent.Id.ToString(), request, default);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        var dto = Assert.IsType<TicketTypeDto>(createdResult.Value);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("VIP Lounge", dto.Name);
        Assert.Equal(60, dto.TotalQuantity);
        Assert.Equal(60, dto.Capacity);
        Assert.Equal(60, dto.RemainingQuantity);
        Assert.Equal(60, dto.AvailableQuantity);
        Assert.Equal(0, dto.SoldQuantity);
        Assert.Equal(0, dto.BookedQuantity);
    }

    [Fact]
    public async Task GetTicketTypes_ReturnsAllTicketTypes_WithAuthoritativeRemainingQuantities()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Tech Summit",
            Description = "Tech conferences",
            Venue = "Hilton Colombo",
            EventDate = DateTime.UtcNow.AddMonths(2),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket1 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Standard",
            Price = 3000m,
            Capacity = 200,
            BookedQuantity = 50, // 150 remaining
        };
        var ticket2 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "VIP",
            Price = 8000m,
            Capacity = 50,
            BookedQuantity = 50, // 0 remaining (sold out)
        };
        context.TicketTypes.AddRange(ticket1, ticket2);
        await context.SaveChangesAsync();

        var controller = CreateController(context, organizerId);

        // Act
        var actionResult = await controller.GetTicketTypes(liveEvent.Id.ToString(), default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var list = Assert.IsAssignableFrom<IReadOnlyList<TicketTypeDto>>(okResult.Value);
        Assert.Equal(2, list.Count);

        var standard = list.First(t => t.Name == "Standard");
        Assert.Equal(200, standard.TotalQuantity);
        Assert.Equal(50, standard.SoldQuantity);
        Assert.Equal(150, standard.RemainingQuantity);

        var vip = list.First(t => t.Name == "VIP");
        Assert.Equal(50, vip.TotalQuantity);
        Assert.Equal(50, vip.SoldQuantity);
        Assert.Equal(0, vip.RemainingQuantity);
    }

    [Fact]
    public async Task GetTicketTypeById_ReturnsSingleTicketType_ForOwningOrganizer()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Food Fair",
            Description = "Culinary festival",
            Venue = "Viharamahadevi Park",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Pass",
            Price = 1000m,
            Capacity = 300,
            BookedQuantity = 75,
        };
        context.TicketTypes.Add(ticket);
        await context.SaveChangesAsync();

        var controller = CreateController(context, organizerId);

        // Act
        var actionResult = await controller.GetTicketTypeById(liveEvent.Id.ToString(), ticket.Id.ToString(), default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<TicketTypeDto>(okResult.Value);
        Assert.Equal(ticket.Id, dto.Id);
        Assert.Equal(300, dto.TotalQuantity);
        Assert.Equal(75, dto.SoldQuantity);
        Assert.Equal(225, dto.RemainingQuantity);
    }

    // =========================================================================
    // 2. AUTHORIZATION & OWNERSHIP ENFORCEMENT
    // =========================================================================

    [Fact]
    public async Task GetTicketTypes_ReturnsForbidden_WhenRequestedByDifferentOrganizer()
    {
        // Arrange
        using var context = CreateContext();
        var ownerOrganizerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = ownerOrganizerId,
            Title = "Private Summit",
            Description = "Summit",
            Venue = "Shangri-La",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var controller = CreateController(context, otherOrganizerId); // Different organizer

        // Act
        var actionResult = await controller.GetTicketTypes(liveEvent.Id.ToString(), default);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(403, objResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketTypeById_ReturnsForbidden_WhenRequestedByDifferentOrganizer()
    {
        // Arrange
        using var context = CreateContext();
        var ownerOrganizerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = ownerOrganizerId,
            Title = "Private Workshop",
            Description = "Workshop",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Member",
            Price = 2500m,
            Capacity = 100,
            BookedQuantity = 20,
        };
        context.TicketTypes.Add(ticket);
        await context.SaveChangesAsync();

        var controller = CreateController(context, otherOrganizerId); // Different organizer

        // Act
        var actionResult = await controller.GetTicketTypeById(liveEvent.Id.ToString(), ticket.Id.ToString(), default);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(403, objResult.StatusCode);
    }

    // =========================================================================
    // 3. PUBLIC CUSTOMER APIS & PRIVACY (No Data Leakage)
    // =========================================================================

    [Fact]
    public async Task GetPublicTicketTypes_ReturnsOnlyCustomerVisibleData_NoPrivateLeakage()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Public Exhibition",
            Description = "Public showcase",
            Venue = "Exhibition Centre",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket1 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "General Entry",
            Price = 1500m,
            Capacity = 100,
            BookedQuantity = 25, // 75 remaining
        };
        var ticket2 = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "VIP Entry",
            Price = 5000m,
            Capacity = 40,
            BookedQuantity = 40, // 0 remaining -> SoldOut
        };
        context.TicketTypes.AddRange(ticket1, ticket2);
        await context.SaveChangesAsync();

        var publicController = CreatePublicController(context);

        // Act
        var actionResult = await publicController.GetPublicTicketTypes(liveEvent.Id.ToString(), default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var publicList = Assert.IsAssignableFrom<IReadOnlyList<PublicTicketTypeDto>>(okResult.Value);
        Assert.Equal(2, publicList.Count);

        var general = publicList.First(t => t.Name == "General Entry");
        Assert.Equal(ticket1.Id, general.Id);
        Assert.Equal("General Entry", general.Name);
        Assert.Equal(1500m, general.Price);
        Assert.Equal(100, general.TotalQuantity);
        Assert.Equal(75, general.RemainingQuantity);
        Assert.Equal(75, general.AvailableQuantity);
        Assert.False(general.IsSoldOut);

        var vip = publicList.First(t => t.Name == "VIP Entry");
        Assert.Equal(40, vip.TotalQuantity);
        Assert.Equal(0, vip.RemainingQuantity);
        Assert.Equal(0, vip.AvailableQuantity);
        Assert.True(vip.IsSoldOut);

        // Verify DTO contract exposes NO private organizer or internal sales details
        var publicType = typeof(PublicTicketTypeDto);
        Assert.Null(publicType.GetProperty("OrganizerId"));
        Assert.Null(publicType.GetProperty("BookedQuantity"));
        Assert.Null(publicType.GetProperty("SoldQuantity"));
        Assert.Null(publicType.GetProperty("BuyerDetails"));
    }

    [Fact]
    public async Task GetPublicTicketTypeById_ReturnsSpecificTicketType_ForCustomer()
    {
        // Arrange
        using var context = CreateContext();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = Guid.NewGuid(),
            Title = "Dance Recital",
            Description = "Classical dance",
            Venue = "Nelum Pokuna",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Balcony Seat",
            Price = 2000m,
            Capacity = 80,
            BookedQuantity = 10,
        };
        context.TicketTypes.Add(ticket);
        await context.SaveChangesAsync();

        var publicController = CreatePublicController(context);

        // Act
        var actionResult = await publicController.GetPublicTicketTypeById(
            liveEvent.Id.ToString(), ticket.Id.ToString(), default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<PublicTicketTypeDto>(okResult.Value);
        Assert.Equal(ticket.Id, dto.Id);
        Assert.Equal("Balcony Seat", dto.Name);
        Assert.Equal(80, dto.TotalQuantity);
        Assert.Equal(70, dto.RemainingQuantity);
        Assert.Equal(70, dto.AvailableQuantity);
        Assert.False(dto.IsSoldOut);
    }

    [Fact]
    public async Task GetPublicTicketTypes_ReturnsNotFound_WhenEventIsNotPublished()
    {
        // Arrange - Approved but not Published event
        using var context = CreateContext();
        var unpublishedEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = Guid.NewGuid(),
            Title = "Draft Gala",
            Description = "Pending publishing",
            Venue = "Town Hall",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Approved, // Not Published
        };
        context.Events.Add(unpublishedEvent);
        await context.SaveChangesAsync();

        var publicController = CreatePublicController(context);

        // Act
        var actionResult = await publicController.GetPublicTicketTypes(unpublishedEvent.Id.ToString(), default);

        // Assert - unpublished event ticket information is never leaked
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    // =========================================================================
    // 4. ACCURACY: UPDATES REFLECTED IMMEDIATELY AT REQUEST TIME
    // =========================================================================

    [Fact]
    public async Task Availability_ReflectsCommittedChangesImmediately_AtRequestTime()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Rock Show",
            Description = "Live Concert",
            Venue = "CR&FC Grounds",
            EventDate = DateTime.UtcNow.AddMonths(1),
            Status = EventStatus.Published,
        };
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "Standing",
            Price = 2500m,
            Capacity = 100,
            BookedQuantity = 0,
        };
        context.TicketTypes.Add(ticket);
        await context.SaveChangesAsync();

        var organizerController = CreateController(context, organizerId);
        var publicController = CreatePublicController(context);

        // Verify initial state: 100 remaining
        var initResult = await publicController.GetPublicTicketTypes(liveEvent.Id.ToString(), default);
        var initOk = Assert.IsType<OkObjectResult>(initResult);
        var initList = Assert.IsAssignableFrom<IReadOnlyList<PublicTicketTypeDto>>(initOk.Value);
        Assert.Equal(100, initList[0].RemainingQuantity);

        // Act: underlying sold quantity increases (e.g. bookings confirmed)
        ticket.BookedQuantity = 40;
        await context.SaveChangesAsync();

        // Assert: Next public read immediately reflects authoritative 60 remaining without stale cache
        var updatedResult = await publicController.GetPublicTicketTypes(liveEvent.Id.ToString(), default);
        var updatedOk = Assert.IsType<OkObjectResult>(updatedResult);
        var updatedList = Assert.IsAssignableFrom<IReadOnlyList<PublicTicketTypeDto>>(updatedOk.Value);
        Assert.Equal(60, updatedList[0].RemainingQuantity);

        // Act: Organizer expands capacity from 100 to 150
        var updateRequest = new UpdateTicketTypeRequest
        {
            Name = "Standing",
            Price = 2500m,
            Capacity = 150,
        };
        var editResult = await organizerController.UpdateTicketType(
            liveEvent.Id.ToString(), ticket.Id.ToString(), updateRequest, default);
        var editOk = Assert.IsType<OkObjectResult>(editResult);
        var editDto = Assert.IsType<TicketTypeDto>(editOk.Value);
        Assert.Equal(150, editDto.TotalQuantity);
        Assert.Equal(110, editDto.RemainingQuantity); // 150 - 40 = 110

        // Assert: Public endpoint immediately reflects new remaining quantity (110)
        var finalResult = await publicController.GetPublicTicketTypes(liveEvent.Id.ToString(), default);
        var finalOk = Assert.IsType<OkObjectResult>(finalResult);
        var finalList = Assert.IsAssignableFrom<IReadOnlyList<PublicTicketTypeDto>>(finalOk.Value);
        Assert.Equal(150, finalList[0].TotalQuantity);
        Assert.Equal(110, finalList[0].RemainingQuantity);
    }
}
