using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventCancellationRequestServiceTests
{
    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    private static Event CreateTestEvent(
        EventStatus status,
        Guid organizerId,
        string title = "Original Title",
        string venue = "Original Venue")
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Original Description for the event",
            Venue = venue,
            EventDate = DateTime.UtcNow.AddDays(15),
            Price = 2500m,
            Category = "Musical Concert",
            VenueType = "Indoor",
            ImageBlobName = "posters/original.jpg",
            CoverBlobName = "covers/original.jpg",
            Status = status,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ReviewedAt = DateTime.UtcNow.AddDays(-4),
            ReviewedBy = Guid.NewGuid()
        };
    }

    // =========================================================================
    // 1. SUCCESSFUL CREATION & LIVE EVENT IMMUTABILITY
    // =========================================================================

    [Fact]
    public async Task SubmitCancellationRequestAsync_ApprovedEvent_CreatesPendingRequest_LeavesLiveEventUntouched()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var request = new SubmitEventCancellationRequest
        {
            Reason = "The venue has experienced unforeseen electrical issues and cannot host the event."
        };

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(liveEvent.Id, request, organizerId);

        // Assert success
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);
        Assert.False(isConflict);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(request.Reason, result.Reason);
        Assert.Equal(liveEvent.Id, result.EventId);
        Assert.Equal(organizerId, result.OrganizerId);

        // CRITICAL: Verify Live Event record is STILL Approved (NOT Cancelled)!
        var reloadedEvent = await context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == liveEvent.Id);
        Assert.NotNull(reloadedEvent);
        Assert.Equal(EventStatus.Approved, reloadedEvent.Status);
        Assert.Equal("Original Title", reloadedEvent.Title);
    }

    [Fact]
    public async Task SubmitCancellationRequestAsync_PublishedEvent_Succeeds()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var request = new SubmitEventCancellationRequest
        {
            Reason = "Artist is unable to travel due to sudden illness."
        };

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(liveEvent.Id, request, organizerId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);

        // Live event remains Published
        var reloadedEvent = await context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == liveEvent.Id);
        Assert.NotNull(reloadedEvent);
        Assert.Equal(EventStatus.Published, reloadedEvent.Status);
    }

    // =========================================================================
    // 2. VALIDATION & SECURITY (404, 403, 409 INVALID STATE)
    // =========================================================================

    [Fact]
    public async Task SubmitCancellationRequestAsync_EventNotFound_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                Guid.NewGuid(),
                new SubmitEventCancellationRequest { Reason = "Valid reason for cancellation" },
                Guid.NewGuid());

        Assert.True(isNotFound);
        Assert.Null(result);
        Assert.Equal("Event not found.", error);
    }

    [Fact]
    public async Task SubmitCancellationRequestAsync_NotOrganizerOwner_ReturnsForbidden()
    {
        using var context = CreateContext();
        var actualOwner = Guid.NewGuid();
        var maliciousCaller = Guid.NewGuid();

        var liveEvent = CreateTestEvent(EventStatus.Approved, actualOwner);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                liveEvent.Id,
                new SubmitEventCancellationRequest { Reason = "Valid reason for cancellation" },
                maliciousCaller);

        Assert.True(isForbidden);
        Assert.Null(result);
        Assert.Equal("You do not have permission to cancel this event.", error);
    }

    [Theory]
    [InlineData(EventStatus.Pending)]
    [InlineData(EventStatus.Rejected)]
    [InlineData(EventStatus.Cancelled)]
    public async Task SubmitCancellationRequestAsync_UnapprovedEvent_ReturnsInvalidState(EventStatus status)
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(status, organizerId);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                liveEvent.Id,
                new SubmitEventCancellationRequest { Reason = "Valid cancellation reason here" },
                organizerId);

        Assert.True(isInvalidState);
        Assert.Null(result);
        Assert.Equal("Cancellation requests can only be submitted for approved or published events.", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad")]
    public async Task SubmitCancellationRequestAsync_InvalidReason_ReturnsValidationError(string? invalidReason)
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                liveEvent.Id,
                new SubmitEventCancellationRequest { Reason = invalidReason! },
                organizerId);

        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);
        Assert.False(isConflict);
        Assert.Null(result);
        Assert.NotNull(error);
    }

    // =========================================================================
    // 3. CONCURRENCY & CROSS-WORKFLOW MUTUAL EXCLUSION
    // =========================================================================

    [Fact]
    public async Task SubmitCancellationRequestAsync_DuplicatePendingRequest_ReturnsConflict()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var existingRequest = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "First cancellation request",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.EventCancellationRequests.Add(existingRequest);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                liveEvent.Id,
                new SubmitEventCancellationRequest { Reason = "Second cancellation request" },
                organizerId);

        Assert.True(isConflict);
        Assert.Null(result);
        Assert.Equal("This event already has a cancellation request pending review.", error);
    }

    [Fact]
    public async Task SubmitCancellationRequestAsync_PendingUpdateRequestExists_ReturnsConflict()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var pendingUpdate = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Title = "New Title",
            Description = "New Description is here",
            Venue = "New Venue",
            EventDate = DateTime.UtcNow.AddDays(20),
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.EventUpdateRequests.Add(pendingUpdate);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitCancellationRequestAsync(
                liveEvent.Id,
                new SubmitEventCancellationRequest { Reason = "Attempting to cancel while update is pending" },
                organizerId);

        Assert.True(isConflict);
        Assert.Null(result);
        Assert.Equal("Cannot submit a cancellation request while an update request is pending review.", error);
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_PendingCancellationRequestExists_ReturnsConflict()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var pendingCancellation = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Existing pending cancellation",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.EventCancellationRequests.Add(pendingCancellation);
        await context.SaveChangesAsync();

        var mockStorage = new Mock<IEventImageStorage>();
        var updateService = new EventUpdateRequestService(context, mockStorage.Object);

        var updateRequest = new SubmitEventUpdateRequest
        {
            Title = "Updated Title",
            Description = "A valid updated description here",
            Venue = "Updated Venue",
            EventDate = DateTime.UtcNow.AddDays(20)
        };

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await updateService.SubmitUpdateRequestAsync(liveEvent.Id, updateRequest, organizerId);

        Assert.True(isConflict);
        Assert.Null(result);
        Assert.Equal("Cannot submit an update request while a cancellation request is pending review.", error);
    }

    // =========================================================================
    // 4. RETRIEVAL (GET CANCELLATION REQUEST)
    // =========================================================================

    [Fact]
    public async Task GetCancellationRequestAsync_ReturnsLatestPendingRequest()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var cancellationRequest = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Severe weather warning issued for the venue date.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-2)
        };
        context.EventCancellationRequests.Add(cancellationRequest);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden) =
            await service.GetCancellationRequestAsync(liveEvent.Id, organizerId);

        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(cancellationRequest.Id, result.Id);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Severe weather warning issued for the venue date.", result.Reason);
    }

    [Fact]
    public async Task GetCancellationRequestAsync_NotOwner_ReturnsForbidden()
    {
        using var context = CreateContext();
        var owner = Guid.NewGuid();
        var nonOwner = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, owner);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden) =
            await service.GetCancellationRequestAsync(liveEvent.Id, nonOwner);

        Assert.True(isForbidden);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCancellationRequestAsync_NoRequestExists_ReturnsNotFound()
    {
        using var context = CreateContext();
        var owner = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, owner);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        var (result, error, isNotFound, isForbidden) =
            await service.GetCancellationRequestAsync(liveEvent.Id, owner);

        Assert.True(isNotFound);
        Assert.Null(result);
    }

    // =========================================================================
    // 5. TICKET SALES TEMPORARY BLOCKING
    // =========================================================================

    [Fact]
    public async Task TicketTypeService_PendingCancellation_BlocksTicketAvailability()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(liveEvent);

        var ticket = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            Name = "General Admission",
            Price = 50m,
            Capacity = 100,
            BookedQuantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        context.TicketTypes.Add(ticket);
        await context.SaveChangesAsync();

        var ticketService = new TicketTypeService(context);

        // First verify normal behavior before cancellation request
        var (normalTickets, normalNotFound) = await ticketService.GetPublicByEventIdAsync(liveEvent.Id);
        Assert.False(normalNotFound);
        Assert.NotNull(normalTickets);
        Assert.Single(normalTickets);
        Assert.Equal(90, normalTickets[0].AvailableQuantity);
        Assert.False(normalTickets[0].IsSoldOut);

        // Now add a Pending cancellation request
        context.EventCancellationRequests.Add(new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Pending review for cancellation",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Verify ticket availability is BLOCKED
        var (blockedTickets, blockedNotFound) = await ticketService.GetPublicByEventIdAsync(liveEvent.Id);
        Assert.False(blockedNotFound);
        Assert.NotNull(blockedTickets);
        Assert.Single(blockedTickets);
        Assert.Equal(0, blockedTickets[0].AvailableQuantity);
        Assert.True(blockedTickets[0].IsSoldOut);
    }

    // =========================================================================
    // 6. CONTROLLER INTEGRATION
    // =========================================================================

    [Fact]
    public async Task Controller_SubmitCancellationRequest_ReturnsCreatedAtAction()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);
        await context.SaveChangesAsync();

        var cancellationService = new EventCancellationRequestService(context);
        var controller = new EventsController(
            context,
            cancellationRequestService: cancellationService);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var request = new SubmitEventCancellationRequest
        {
            Reason = "Valid cancellation request reason."
        };

        var response = await controller.SubmitCancellationRequest(liveEvent.Id.ToString(), request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(response);
        var resultDto = Assert.IsType<EventCancellationRequestDto>(createdResult.Value);
        Assert.Equal("Pending", resultDto.Status);
        Assert.Equal(request.Reason, resultDto.Reason);
    }

    [Fact]
    public async Task Controller_GetCancellationRequest_ReturnsOk()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var cancellationRequest = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Valid cancellation reason.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(cancellationRequest);
        await context.SaveChangesAsync();

        var cancellationService = new EventCancellationRequestService(context);
        var controller = new EventsController(
            context,
            cancellationRequestService: cancellationService);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
            new Claim(ClaimTypes.Role, "Organizer")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var response = await controller.GetCancellationRequest(liveEvent.Id.ToString(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var resultDto = Assert.IsType<EventCancellationRequestDto>(okResult.Value);
        Assert.Equal(cancellationRequest.Id, resultDto.Id);
        Assert.Equal("Pending", resultDto.Status);
    }
}
