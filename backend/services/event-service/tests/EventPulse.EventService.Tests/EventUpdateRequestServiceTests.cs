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

public class EventUpdateRequestServiceTests
{
    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    private static Mock<IEventImageStorage> MockStorage()
    {
        var mock = new Mock<IEventImageStorage>();
        mock.Setup(m => m.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string _, string fileName, string folder, CancellationToken _)
                => $"{folder}/{Guid.NewGuid()}-{fileName}");
        mock.Setup(m => m.GetPublicUrl(It.IsAny<string?>()))
            .Returns((string? blob) => blob is null ? null : $"http://127.0.0.1:10000/devstoreaccount1/{blob}");
        mock.Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static IFormFile MakeFormFile(string contentType, long sizeBytes, string fileName = "test.jpg")
    {
        var bytes = new byte[Math.Max(sizeBytes, 1)];
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(sizeBytes);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    private static Event CreateTestEvent(
        EventStatus status,
        Guid organizerId,
        string title = "Original Title",
        string venue = "Original Venue",
        DateTime? date = null,
        string? imageBlob = "posters/original.jpg",
        string? coverBlob = "covers/original.jpg")
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Original Description for the event",
            Venue = venue,
            EventDate = date ?? DateTime.UtcNow.AddDays(15),
            Price = 2500m,
            Category = "Musical Concert",
            VenueType = "Indoor",
            ImageBlobName = imageBlob,
            CoverBlobName = coverBlob,
            Status = status,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ReviewedAt = DateTime.UtcNow.AddDays(-4),
            ReviewedBy = Guid.NewGuid()
        };
    }

    private static SubmitEventUpdateRequest ValidUpdateRequest() => new()
    {
        Title = "Updated Festival Title",
        Description = "An updated description of the event with extensive detail.",
        Venue = "Grand Auditorium Center",
        EventDate = DateTime.UtcNow.AddDays(30),
        Category = "Festival",
        VenueType = "Indoor"
    };

    // =========================================================================
    // 1. SUCCESSFUL CREATION & LIVE EVENT IMMUTABILITY
    // =========================================================================

    [Fact]
    public async Task SubmitUpdateRequestAsync_ApprovedEvent_CreatesPendingUpdateRequest_LeavesLiveEventUntouched()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var originalEvent = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(originalEvent);
        await context.SaveChangesAsync();

        var originalTitle = originalEvent.Title;
        var originalVenue = originalEvent.Venue;
        var originalDate = originalEvent.EventDate;
        var originalStatus = originalEvent.Status;

        var request = ValidUpdateRequest();

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(originalEvent.Id, request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);
        Assert.False(isConflict);
        Assert.NotNull(result);

        Assert.Equal("Pending", result.Status);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(request.Venue, result.Venue);
        Assert.True(result.HasVenueChanged);
        Assert.True(result.HasDateChanged);
        Assert.True(result.IsMajorChange);

        // Verify that the Live Event record in DB was NOT modified
        var freshEvent = await context.Events.AsNoTracking().FirstAsync(e => e.Id == originalEvent.Id);
        Assert.Equal(originalTitle, freshEvent.Title);
        Assert.Equal(originalVenue, freshEvent.Venue);
        Assert.Equal(originalDate, freshEvent.EventDate);
        Assert.Equal(originalStatus, freshEvent.Status);

        // Verify the EventUpdateRequest was saved in DB
        var savedRequest = await context.EventUpdateRequests.FirstOrDefaultAsync(r => r.EventId == originalEvent.Id);
        Assert.NotNull(savedRequest);
        Assert.Equal(EventUpdateRequestStatus.Pending, savedRequest.Status);
        Assert.Equal(request.Title, savedRequest.Title);
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_PublishedEvent_Succeeds()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var originalEvent = CreateTestEvent(EventStatus.Published, organizerId);
        context.Events.Add(originalEvent);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(originalEvent.Id, request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
    }

    // =========================================================================
    // 2. STATE REJECTIONS
    // =========================================================================

    [Theory]
    [InlineData(EventStatus.Pending)]
    [InlineData(EventStatus.Rejected)]
    public async Task SubmitUpdateRequestAsync_IneligibleStatus_ReturnsInvalidState(EventStatus invalidStatus)
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(invalidStatus, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, organizerId);

        // Assert
        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.Contains("approved or published", error, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 3. AUTHORIZATION & OWNERSHIP
    // =========================================================================

    [Fact]
    public async Task SubmitUpdateRequestAsync_NonOwner_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, otherOrganizerId);

        // Assert
        Assert.Null(result);
        Assert.True(isForbidden);
        Assert.Contains("permission", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_NonExistentEvent_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(Guid.NewGuid(), ValidUpdateRequest(), Guid.NewGuid());

        // Assert
        Assert.Null(result);
        Assert.True(isNotFound);
    }

    // =========================================================================
    // 4. DUPLICATE PENDING REQUEST CONFLICT
    // =========================================================================

    [Fact]
    public async Task SubmitUpdateRequestAsync_ExistingPendingRequest_ReturnsConflict()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);

        var existingPending = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = organizerId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            Title = "Prior pending update",
            Description = "Prior pending description",
            Venue = "Prior pending venue",
            EventDate = DateTime.UtcNow.AddDays(20)
        };
        context.EventUpdateRequests.Add(existingPending);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, organizerId);

        // Assert
        Assert.Null(result);
        Assert.True(isConflict);
        Assert.Contains("already has an update request pending review", error, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 5. DOMAIN VALIDATION
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task SubmitUpdateRequestAsync_InvalidTitle_ReturnsValidationError(string title)
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);
        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();
        request.Title = title;

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, organizerId);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_PastDate_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);
        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();
        request.EventDate = DateTime.UtcNow.AddDays(-1);

        var (result, error, _, _, _, _) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, organizerId);

        Assert.Null(result);
        Assert.Contains("future", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_InvalidCategory_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);
        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();
        request.Category = "NonExistentCategory";

        var (result, error, _, _, _, _) =
            await service.SubmitUpdateRequestAsync(ev.Id, request, organizerId);

        Assert.Null(result);
        Assert.Contains("Please select a valid event category.", error);
    }

    // =========================================================================
    // 6. SAFE REPLACEMENT IMAGES
    // =========================================================================

    [Fact]
    public async Task SubmitUpdateRequestAsync_WithReplacementImages_UploadsBlobsAndPreservesOriginalEventBlobs()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var originalEvent = CreateTestEvent(EventStatus.Approved, organizerId, imageBlob: "original-poster.jpg", coverBlob: "original-cover.jpg");
        context.Events.Add(originalEvent);
        await context.SaveChangesAsync();

        var request = ValidUpdateRequest();
        request.Image = MakeFormFile("image/png", 2048, "new-poster.png");
        request.CoverImage = MakeFormFile("image/webp", 4096, "new-cover.webp");

        // Act
        var (result, error, _, _, _, _) =
            await service.SubmitUpdateRequestAsync(originalEvent.Id, request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageUrl);
        Assert.NotNull(result.CoverUrl);
        Assert.True(result.HasImageChanged);
        Assert.True(result.HasCoverChanged);

        // Storage UploadAsync was called for both images
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "image/png", "new-poster.png", "event-posters", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "image/webp", "new-cover.webp", "event-covers", It.IsAny<CancellationToken>()), Times.Once);

        // Original blobs were NEVER deleted
        storage.Verify(s => s.DeleteAsync("original-poster.jpg", It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.DeleteAsync("original-cover.jpg", It.IsAny<CancellationToken>()), Times.Never);

        // Original event record still holds the original blob names
        var freshEvent = await context.Events.AsNoTracking().FirstAsync(e => e.Id == originalEvent.Id);
        Assert.Equal("original-poster.jpg", freshEvent.ImageBlobName);
        Assert.Equal("original-cover.jpg", freshEvent.CoverBlobName);
    }

    // =========================================================================
    // 7. GET UPDATE REQUEST & CHANGE FLAGS
    // =========================================================================

    [Fact]
    public async Task GetUpdateRequestAsync_ReturnsExistingUpdateRequest_WithChangeFlags()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var organizerId = Guid.NewGuid();
        var originalDate = new DateTime(2027, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var ev = CreateTestEvent(EventStatus.Approved, organizerId, title: "Same Title", venue: "Old Arena", date: originalDate);
        context.Events.Add(ev);

        var updateReq = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = organizerId,
            Status = EventUpdateRequestStatus.Pending,
            Title = "Same Title",
            Description = ev.Description,
            Venue = "New Arena", // Venue changed
            EventDate = originalDate,
            Category = ev.Category,
            VenueType = ev.VenueType,
            ImageBlobName = ev.ImageBlobName,
            CoverBlobName = ev.CoverBlobName,
            RequestedAt = DateTime.UtcNow
        };
        context.EventUpdateRequests.Add(updateReq);
        await context.SaveChangesAsync();

        // Act
        var (result, error, isNotFound, isForbidden) =
            await service.GetUpdateRequestAsync(ev.Id, organizerId);

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.False(result.HasTitleChanged);
        Assert.True(result.HasVenueChanged);
        Assert.False(result.HasDateChanged);
        Assert.True(result.IsMajorChange); // Venue changed is a major change
    }

    [Fact]
    public async Task GetUpdateRequestAsync_NonOwner_ReturnsForbidden()
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);

        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var (result, error, isNotFound, isForbidden) =
            await service.GetUpdateRequestAsync(ev.Id, Guid.NewGuid());

        Assert.Null(result);
        Assert.True(isForbidden);
    }

    [Fact]
    public async Task GetUpdateRequestAsync_NoUpdateRequest_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);

        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var (result, error, isNotFound, isForbidden) =
            await service.GetUpdateRequestAsync(ev.Id, organizerId);

        Assert.Null(result);
        Assert.True(isNotFound);
    }

    // =========================================================================
    // 8. CONTROLLER ENDPOINT INTEGRATION
    // =========================================================================

    private static ControllerContext CreateControllerContext(Guid userId, string role = "Organizer")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task Controller_SubmitUpdateRequest_ReturnsCreatedAtAction_OnSuccess()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var mockService = new Mock<IEventUpdateRequestService>();
        var expectedDto = new EventUpdateRequestDto
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            Status = "Pending",
            Title = "Updated Title",
            Venue = "Updated Venue",
            EventDate = DateTime.UtcNow.AddDays(30)
        };

        mockService.Setup(s => s.SubmitUpdateRequestAsync(ev.Id, It.IsAny<SubmitEventUpdateRequest>(), organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedDto, null, false, false, false, false));

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(organizerId)
        };

        // Act
        var actionResult = await controller.SubmitUpdateRequest(ev.Id.ToString(), ValidUpdateRequest(), CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        var dto = Assert.IsType<EventUpdateRequestDto>(createdResult.Value);
        Assert.Equal(expectedDto.Id, dto.Id);
    }

    [Fact]
    public async Task Controller_SubmitUpdateRequest_ReturnsConflict_OnPendingDuplicate()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, organizerId);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var mockService = new Mock<IEventUpdateRequestService>();
        mockService.Setup(s => s.SubmitUpdateRequestAsync(ev.Id, It.IsAny<SubmitEventUpdateRequest>(), organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "This event already has an update request pending review.", false, false, false, true));

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(organizerId)
        };

        // Act
        var actionResult = await controller.SubmitUpdateRequest(ev.Id.ToString(), ValidUpdateRequest(), CancellationToken.None);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task Controller_GetUpdateRequest_ReturnsOkWithDto()
    {
        // Arrange
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var mockService = new Mock<IEventUpdateRequestService>();
        var expectedDto = new EventUpdateRequestDto
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = "Pending",
            Title = "Updated Title"
        };

        mockService.Setup(s => s.GetUpdateRequestAsync(eventId, organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedDto, null, false, false));

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(organizerId)
        };

        // Act
        var actionResult = await controller.GetUpdateRequest(eventId.ToString(), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<EventUpdateRequestDto>(okResult.Value);
        Assert.Equal(expectedDto.Id, dto.Id);
    }

    // =========================================================================
    // 9. ADMIN REVIEW FLOW TESTS (EP-210 / US-14)
    // =========================================================================

    [Fact]
    public async Task GetPendingUpdateRequestsAsync_ReturnsOnlyPendingRequests_OrderedByRequestedAtAscending()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var orgId = Guid.NewGuid();
        var ev1 = CreateTestEvent(EventStatus.Approved, orgId, title: "Event 1");
        var ev2 = CreateTestEvent(EventStatus.Approved, orgId, title: "Event 2");
        var ev3 = CreateTestEvent(EventStatus.Approved, orgId, title: "Event 3");
        context.Events.AddRange(ev1, ev2, ev3);

        // Request 1: Pending, older
        var req1 = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev1.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddMinutes(-20),
            Title = "Updated Event 1",
            Description = "Description 1",
            Venue = "New Venue 1",
            EventDate = DateTime.UtcNow.AddDays(20)
        };

        // Request 2: Pending, newer
        var req2 = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev2.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            Title = "Updated Event 2",
            Description = "Description 2",
            Venue = "New Venue 2",
            EventDate = DateTime.UtcNow.AddDays(25)
        };

        // Request 3: Already Approved (should not appear in pending list)
        var req3 = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev3.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Approved,
            RequestedAt = DateTime.UtcNow.AddMinutes(-60),
            ReviewedAt = DateTime.UtcNow.AddMinutes(-30),
            ReviewedBy = Guid.NewGuid(),
            Title = "Updated Event 3",
            Description = "Description 3",
            Venue = "New Venue 3",
            EventDate = DateTime.UtcNow.AddDays(30)
        };

        context.EventUpdateRequests.AddRange(req1, req2, req3);
        await context.SaveChangesAsync();

        // Act
        var pending = await service.GetPendingUpdateRequestsAsync();

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.Equal(req1.Id, pending[0].Id);
        Assert.Equal(req2.Id, pending[1].Id);
    }

    [Fact]
    public async Task GetUpdateRequestReviewAsync_ReturnsComparisonDto_WithChangeFlagsAndMajorChange()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var orgId = Guid.NewGuid();
        var originalDate = new DateTime(2027, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var newDate = new DateTime(2027, 8, 15, 12, 0, 0, DateTimeKind.Utc);

        var ev = CreateTestEvent(EventStatus.Approved, orgId, title: "Rockfest", venue: "Colombo Park", date: originalDate);
        context.Events.Add(ev);

        var req = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Title = "Rockfest 2027 - Expanded", // Title changed
            Description = ev.Description,      // Unchanged
            Venue = "Nelum Pokuna",             // Venue changed (Major)
            EventDate = newDate,                // Date changed (Major)
            Category = ev.Category,
            VenueType = ev.VenueType,
            ImageBlobName = ev.ImageBlobName,
            CoverBlobName = ev.CoverBlobName
        };
        context.EventUpdateRequests.Add(req);
        await context.SaveChangesAsync();

        // Act
        var comparison = await service.GetUpdateRequestReviewAsync(req.Id);

        // Assert
        Assert.NotNull(comparison);
        Assert.Equal(req.Id, comparison.Id);
        Assert.Equal(ev.Id, comparison.EventId);
        Assert.Equal("Pending", comparison.Status);

        // Current values
        Assert.Equal("Rockfest", comparison.Current.Title);
        Assert.Equal("Colombo Park", comparison.Current.Venue);
        Assert.Equal(originalDate, comparison.Current.EventDate);

        // Proposed values
        Assert.Equal("Rockfest 2027 - Expanded", comparison.Proposed.Title);
        Assert.Equal("Nelum Pokuna", comparison.Proposed.Venue);
        Assert.Equal(newDate, comparison.Proposed.EventDate);

        // Change flags
        Assert.True(comparison.HasTitleChanged);
        Assert.False(comparison.HasDescriptionChanged);
        Assert.True(comparison.HasVenueChanged);
        Assert.True(comparison.HasDateChanged);
        Assert.True(comparison.IsMajorChange);
    }

    [Fact]
    public async Task ApproveUpdateRequestAsync_AppliesChangesToLiveEvent_Atomically_AndMarksApproved()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var originalDate = new DateTime(2027, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var newDate = new DateTime(2027, 10, 10, 18, 0, 0, DateTimeKind.Utc);

        var ev = CreateTestEvent(EventStatus.Approved, orgId, title: "Tech Summit", venue: "Old Hall", date: originalDate);
        context.Events.Add(ev);

        var req = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            Title = "Global Tech Summit",
            Description = "Updated extensive description",
            Venue = "Grand Ballroom",
            EventDate = newDate,
            Category = "Conference",
            VenueType = "Indoor",
            ImageBlobName = "posters/new-poster.jpg",
            CoverBlobName = "covers/new-cover.jpg"
        };
        context.EventUpdateRequests.Add(req);
        await context.SaveChangesAsync();

        // Act
        var (result, error, isNotFound, isInvalidState) =
            await service.ApproveUpdateRequestAsync(req.Id, adminId, "Approved after verifying venue booking.");

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isInvalidState);
        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);

        // 1. Verify Live Event in database was atomically updated
        var freshEvent = await context.Events.AsNoTracking().FirstAsync(e => e.Id == ev.Id);
        Assert.Equal("Global Tech Summit", freshEvent.Title);
        Assert.Equal("Updated extensive description", freshEvent.Description);
        Assert.Equal("Grand Ballroom", freshEvent.Venue);
        Assert.Equal(newDate, freshEvent.EventDate);
        Assert.Equal("Conference", freshEvent.Category);
        Assert.Equal("Indoor", freshEvent.VenueType);
        Assert.Equal("posters/new-poster.jpg", freshEvent.ImageBlobName);
        Assert.Equal("covers/new-cover.jpg", freshEvent.CoverBlobName);

        // 2. Verify EventUpdateRequest was marked Approved with audit fields
        var freshReq = await context.EventUpdateRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id);
        Assert.Equal(EventUpdateRequestStatus.Approved, freshReq.Status);
        Assert.NotNull(freshReq.ReviewedAt);
        Assert.Equal(adminId, freshReq.ReviewedBy);
        Assert.Equal("Approved after verifying venue booking.", freshReq.ReviewComment);
    }

    [Fact]
    public async Task ApproveUpdateRequestAsync_WhenAlreadyApprovedOrRejected_ReturnsInvalidState()
    {
        // Arrange
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);

        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, orgId);
        context.Events.Add(ev);

        var req = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Approved, // Already reviewed!
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            ReviewedAt = DateTime.UtcNow.AddHours(-1),
            ReviewedBy = adminId,
            Title = "Updated Title",
            Description = "Updated Description",
            Venue = "Updated Venue",
            EventDate = DateTime.UtcNow.AddDays(20)
        };
        context.EventUpdateRequests.Add(req);
        await context.SaveChangesAsync();

        // Act
        var (result, error, isNotFound, isInvalidState) =
            await service.ApproveUpdateRequestAsync(req.Id, adminId);

        // Assert
        Assert.Null(result);
        Assert.False(isNotFound);
        Assert.True(isInvalidState);
        Assert.Contains("Only Pending update requests can be approved", error);
    }

    [Fact]
    public async Task RejectUpdateRequestAsync_LeavesLiveEventUnchanged_RecordsRejectionReason_AndMarksRejected()
    {
        // Arrange
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventUpdateRequestService(context, storage.Object);

        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var originalDate = new DateTime(2027, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var originalVenue = "Original Arena";
        var originalTitle = "Original Concert";

        var ev = CreateTestEvent(EventStatus.Approved, orgId, title: originalTitle, venue: originalVenue, date: originalDate);
        context.Events.Add(ev);

        var req = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow.AddHours(-3),
            Title = "Proposed Title Change",
            Description = "Proposed Description Change",
            Venue = "Proposed Different Venue",
            EventDate = originalDate.AddDays(10)
        };
        context.EventUpdateRequests.Add(req);
        await context.SaveChangesAsync();

        // Act
        var (result, error, isNotFound, isInvalidState) =
            await service.RejectUpdateRequestAsync(req.Id, adminId, "Venue details could not be verified with the venue manager.");

        // Assert
        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isInvalidState);
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);

        // 1. Verify Live Event remains completely UNCHANGED
        var freshEvent = await context.Events.AsNoTracking().FirstAsync(e => e.Id == ev.Id);
        Assert.Equal(originalTitle, freshEvent.Title);
        Assert.Equal(originalVenue, freshEvent.Venue);
        Assert.Equal(originalDate, freshEvent.EventDate);

        // 2. Verify EventUpdateRequest is marked Rejected with notes
        var freshReq = await context.EventUpdateRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id);
        Assert.Equal(EventUpdateRequestStatus.Rejected, freshReq.Status);
        Assert.NotNull(freshReq.ReviewedAt);
        Assert.Equal(adminId, freshReq.ReviewedBy);
        Assert.Equal("Venue details could not be verified with the venue manager.", freshReq.ReviewComment);
    }

    [Fact]
    public async Task RejectUpdateRequestAsync_WithoutNotes_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);

        var (result, error, isNotFound, isInvalidState) =
            await service.RejectUpdateRequestAsync(Guid.NewGuid(), Guid.NewGuid(), "   ");

        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.Contains("Rejection feedback is required", error);
    }

    [Fact]
    public async Task RejectUpdateRequestAsync_WhenAlreadyReviewed_ReturnsInvalidState()
    {
        // Arrange
        using var context = CreateContext();
        var service = new EventUpdateRequestService(context, MockStorage().Object);

        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var ev = CreateTestEvent(EventStatus.Approved, orgId);
        context.Events.Add(ev);

        var req = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            OrganizerId = orgId,
            Status = EventUpdateRequestStatus.Rejected, // Already rejected
            RequestedAt = DateTime.UtcNow.AddHours(-2),
            ReviewedAt = DateTime.UtcNow.AddHours(-1),
            ReviewedBy = adminId,
            Title = "Updated Title",
            Description = "Updated Description",
            Venue = "Updated Venue",
            EventDate = DateTime.UtcNow.AddDays(20)
        };
        context.EventUpdateRequests.Add(req);
        await context.SaveChangesAsync();

        // Act
        var (result, error, isNotFound, isInvalidState) =
            await service.RejectUpdateRequestAsync(req.Id, adminId, "Second rejection attempt.");

        // Assert
        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.Contains("Only Pending update requests can be rejected", error);
    }

    // =========================================================================
    // 10. CONTROLLER ADMIN ENDPOINT INTEGRATION TESTS
    // =========================================================================

    [Fact]
    public async Task Controller_GetPendingUpdateRequests_ReturnsOkWithList()
    {
        // Arrange
        using var context = CreateContext();
        var adminId = Guid.NewGuid();
        var mockService = new Mock<IEventUpdateRequestService>();

        var list = new List<AdminEventUpdateComparisonDto>
        {
            new() { Id = Guid.NewGuid(), Status = "Pending" },
            new() { Id = Guid.NewGuid(), Status = "Pending" }
        };

        mockService.Setup(s => s.GetPendingUpdateRequestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(adminId, "Administrator")
        };

        // Act
        var result = await controller.GetPendingUpdateRequests(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<AdminEventUpdateComparisonDto>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task Controller_ApproveUpdateRequest_ReturnsOk()
    {
        // Arrange
        using var context = CreateContext();
        var adminId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var mockService = new Mock<IEventUpdateRequestService>();

        var comparisonDto = new AdminEventUpdateComparisonDto
        {
            Id = reqId,
            Status = "Approved"
        };

        mockService.Setup(s => s.ApproveUpdateRequestAsync(reqId, adminId, "Looks good", It.IsAny<CancellationToken>()))
            .ReturnsAsync((comparisonDto, null, false, false));

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(adminId, "Administrator")
        };

        // Act
        var result = await controller.ApproveUpdateRequest(reqId.ToString(), new ReviewEventRequest { Notes = "Looks good" }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<AdminEventUpdateComparisonDto>(okResult.Value);
        Assert.Equal("Approved", returned.Status);
    }

    [Fact]
    public async Task Controller_ApproveUpdateRequest_WhenAlreadyReviewed_ReturnsConflict()
    {
        // Arrange
        using var context = CreateContext();
        var adminId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var mockService = new Mock<IEventUpdateRequestService>();

        mockService.Setup(s => s.ApproveUpdateRequestAsync(reqId, adminId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "Only Pending update requests can be approved. Current status: Approved.", false, true));

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(adminId, "Administrator")
        };

        // Act
        var result = await controller.ApproveUpdateRequest(reqId.ToString(), new ReviewEventRequest(), CancellationToken.None);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task Controller_RejectUpdateRequest_WithoutNotes_ReturnsBadRequest()
    {
        // Arrange
        using var context = CreateContext();
        var adminId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var mockService = new Mock<IEventUpdateRequestService>();

        var controller = new EventsController(context, updateRequestService: mockService.Object)
        {
            ControllerContext = CreateControllerContext(adminId, "Administrator")
        };

        // Act (no notes provided)
        var result = await controller.RejectUpdateRequest(reqId.ToString(), new ReviewEventRequest { Notes = "  " }, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }
}

