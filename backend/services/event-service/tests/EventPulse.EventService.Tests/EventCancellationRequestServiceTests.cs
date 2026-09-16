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

    // =========================================================================
    // 8. PHASE 3 — ADMIN REVIEW: GET PENDING & GET BY ID
    // =========================================================================

    [Fact]
    public async Task GetPendingCancellationRequestsAsync_ReturnsOnlyPending_ChronologicalOrder()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var event1 = CreateTestEvent(EventStatus.Approved, organizerId, "Event 1");
        var event2 = CreateTestEvent(EventStatus.Published, organizerId, "Event 2");
        var event3 = CreateTestEvent(EventStatus.Approved, organizerId, "Event 3");
        context.Events.AddRange(event1, event2, event3);

        var req1 = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = event1.Id,
            OrganizerId = organizerId,
            Reason = "Older pending request",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-2)
        };
        var req2 = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = event2.Id,
            OrganizerId = organizerId,
            Reason = "Newer pending request",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-1)
        };
        var req3 = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = event3.Id,
            OrganizerId = organizerId,
            Reason = "Already rejected request",
            Status = EventCancellationRequestStatus.Rejected,
            RequestedAt = DateTime.UtcNow.AddHours(-3)
        };
        context.EventCancellationRequests.AddRange(req1, req2, req3);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var result = await service.GetPendingCancellationRequestsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(req1.Id, result[0].Id);
        Assert.Equal(req2.Id, result[1].Id);
        Assert.Equal("Event 1", result[0].EventTitle);
        Assert.Equal("Event 2", result[1].EventTitle);
    }

    [Fact]
    public async Task GetCancellationRequestReviewAsync_CalculatesTicketsSoldAndCapacity()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(liveEvent);

        var ticket1 = new TicketType { Id = Guid.NewGuid(), EventId = liveEvent.Id, Name = "VIP", Price = 5000m, Capacity = 100, BookedQuantity = 45 };
        var ticket2 = new TicketType { Id = Guid.NewGuid(), EventId = liveEvent.Id, Name = "General", Price = 2500m, Capacity = 300, BookedQuantity = 120 };
        context.TicketTypes.AddRange(ticket1, ticket2);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Unable to secure headliner artist.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var review = await service.GetCancellationRequestReviewAsync(req.Id);

        Assert.NotNull(review);
        Assert.Equal(req.Id, review.Id);
        Assert.Equal(liveEvent.Id, review.EventId);
        Assert.Equal("Unable to secure headliner artist.", review.Reason);
        Assert.Equal(165, review.TotalTicketsSold); // 45 + 120
        Assert.Equal(400, review.TotalCapacity); // 100 + 300
    }

    // =========================================================================
    // 9. PHASE 3 — ADMIN REVIEW: APPROVE CANCELLATION
    // =========================================================================

    [Fact]
    public async Task ApproveCancellationRequestAsync_HappyPath_TransitionsEventToCancelled_Atomically()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Artist fell ill.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var (result, error, isNotFound, isInvalidState) = await service.ApproveCancellationRequestAsync(
            req.Id, adminId, "Cancellation approved after organizer consultation.");

        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isInvalidState);
        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.Equal("Cancelled", result.EventStatus);
        Assert.Equal(adminId, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal("Cancellation approved after organizer consultation.", result.ReviewComment);

        // Verify database state directly
        var dbEvent = await context.Events.FindAsync(liveEvent.Id);
        var dbReq = await context.EventCancellationRequests.FindAsync(req.Id);
        Assert.Equal(EventStatus.Cancelled, dbEvent!.Status);
        Assert.Equal(EventCancellationRequestStatus.Approved, dbReq!.Status);
        Assert.Equal(adminId, dbReq.ReviewedBy);
        Assert.NotNull(dbReq.ReviewedAt);
    }

    [Fact]
    public async Task ApproveCancellationRequestAsync_AlreadyApprovedOrRejected_ReturnsConflict()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Cancelled, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Already reviewed.",
            Status = EventCancellationRequestStatus.Approved,
            RequestedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow,
            ReviewedBy = adminId
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var (result, error, isNotFound, isInvalidState) = await service.ApproveCancellationRequestAsync(req.Id, adminId);

        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.Contains("Only Pending cancellation requests can be approved", error);
    }

    [Fact]
    public async Task ApproveCancellationRequestAsync_NonExistentRequest_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new EventCancellationRequestService(context);
        var (result, error, isNotFound, isInvalidState) = await service.ApproveCancellationRequestAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
        Assert.True(isNotFound);
        Assert.False(isInvalidState);
    }

    // =========================================================================
    // 10. PHASE 3 — ADMIN REVIEW: REJECT CANCELLATION
    // =========================================================================

    [Fact]
    public async Task RejectCancellationRequestAsync_HappyPath_LeavesEventApproved_SetsRequestRejected()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Venue AC problem.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var (result, error, isNotFound, isInvalidState) = await service.RejectCancellationRequestAsync(
            req.Id, adminId, "Alternative cooling equipment will be provided. Event can proceed.");

        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isInvalidState);
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("Approved", result.EventStatus); // Event remains Approved!
        Assert.Equal(adminId, result.ReviewedBy);
        Assert.Equal("Alternative cooling equipment will be provided. Event can proceed.", result.ReviewComment);

        // Verify in DB
        var dbEvent = await context.Events.FindAsync(liveEvent.Id);
        var dbReq = await context.EventCancellationRequests.FindAsync(req.Id);
        Assert.Equal(EventStatus.Approved, dbEvent!.Status);
        Assert.Equal(EventCancellationRequestStatus.Rejected, dbReq!.Status);
    }

    [Fact]
    public async Task RejectCancellationRequestAsync_MissingNotes_ReturnsInvalidState()
    {
        using var context = CreateContext();
        var service = new EventCancellationRequestService(context);
        var (result, error, isNotFound, isInvalidState) = await service.RejectCancellationRequestAsync(Guid.NewGuid(), Guid.NewGuid(), "   ");

        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.Equal("Rejection feedback is required.", error);
    }

    [Fact]
    public async Task Concurrency_DoubleReview_ApproveThenReject_SecondActionFailsCleanly()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminA = Guid.NewGuid();
        var adminB = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Conflicting bookings.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);

        // Admin A approves
        var (approveResult, approveErr, _, _) = await service.ApproveCancellationRequestAsync(req.Id, adminA);
        Assert.NotNull(approveResult);
        Assert.Null(approveErr);

        // Admin B attempts reject on same request
        var (rejectResult, rejectErr, isNotFound, isInvalidState) = await service.RejectCancellationRequestAsync(
            req.Id, adminB, "Attempting to reject already approved request.");

        Assert.Null(rejectResult);
        Assert.True(isInvalidState);
        Assert.Contains("Only Pending cancellation requests can be rejected", rejectErr);

        // Event remains Cancelled
        var dbEvent = await context.Events.FindAsync(liveEvent.Id);
        Assert.Equal(EventStatus.Cancelled, dbEvent!.Status);
    }

    // =========================================================================
    // 11. PHASE 3 — CONTROLLER ADMIN ENDPOINTS
    // =========================================================================

    [Fact]
    public async Task Controller_AdminGetPending_ReturnsOk()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Admin test reason.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var controller = new EventsController(context, cancellationRequestService: service);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "Administrator")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var actionResult = await controller.GetPendingCancellationRequests(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AdminEventCancellationReviewDto>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task Controller_AdminApprove_ReturnsOk()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var liveEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(liveEvent);

        var req = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = liveEvent.Id,
            OrganizerId = organizerId,
            Reason = "Admin approve test.",
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.EventCancellationRequests.Add(req);
        await context.SaveChangesAsync();

        var service = new EventCancellationRequestService(context);
        var controller = new EventsController(context, cancellationRequestService: service);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "Administrator")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var response = await controller.ApproveCancellationRequest(req.Id.ToString(), new ReviewEventRequest { Notes = "Approved" });
        var okResult = Assert.IsType<OkObjectResult>(response);
        var resultDto = Assert.IsType<AdminEventCancellationReviewDto>(okResult.Value);
        Assert.Equal("Approved", resultDto.Status);
        Assert.Equal("Cancelled", resultDto.EventStatus);
    }

    [Fact]
    public async Task Controller_AdminReject_MissingNotes_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var service = new EventCancellationRequestService(context);
        var controller = new EventsController(context, cancellationRequestService: service);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Administrator")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };

        var response = await controller.RejectCancellationRequest(Guid.NewGuid().ToString(), new ReviewEventRequest { Notes = "   " });
        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.NotNull(badRequest.Value);
    }
}
