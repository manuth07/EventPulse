using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventReviewServiceTests
{
    private class FakeImageStorage : IEventImageStorage
    {
        public Task<string> UploadAsync(Stream imageStream, string contentType, string originalFileName, CancellationToken cancellationToken = default)
            => Task.FromResult("events/fake.webp");

        public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string? GetPublicUrl(string? blobName)
            => string.IsNullOrEmpty(blobName) ? null : $"http://127.0.0.1:10000/devstoreaccount1/event-posters/{blobName}";
    }

    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    private static Event MakeEvent(
        EventStatus status,
        string title = "Test Event",
        DateTime? createdAt = null,
        string? blobName = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = "Description for " + title,
        Venue = "Venue 1",
        EventDate = DateTime.UtcNow.AddDays(14),
        Price = 1500m,
        Status = status,
        OrganizerId = Guid.NewGuid(),
        CreatedAt = createdAt ?? DateTime.UtcNow,
        ImageBlobName = blobName,
    };

    // =========================================================================
    // GetPendingEventsAsync Tests
    // =========================================================================

    [Fact]
    public async Task GetPendingEventsAsync_ReturnsOnlyPendingEvents_OrderedByCreatedAtAscending()
    {
        using var context = CreateContext();
        var pendingOld = MakeEvent(EventStatus.Pending, "Pending Old", DateTime.UtcNow.AddDays(-2));
        var pendingNew = MakeEvent(EventStatus.Pending, "Pending New", DateTime.UtcNow.AddDays(-1));
        var approved = MakeEvent(EventStatus.Approved, "Approved");
        var rejected = MakeEvent(EventStatus.Rejected, "Rejected");
        var published = MakeEvent(EventStatus.Published, "Published");

        context.Events.AddRange(pendingNew, approved, rejected, pendingOld, published);
        await context.SaveChangesAsync();

        var service = new EventReviewService(context, new FakeImageStorage(), null!);
        var result = await service.GetPendingEventsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Pending Old", result[0].Title);
        Assert.Equal("Pending New", result[1].Title);
        Assert.All(result, item => Assert.Equal("Pending", item.Status));
    }

    [Fact]
    public async Task GetPendingEventsAsync_WhenNoPendingEvents_ReturnsEmptyList()
    {
        using var context = CreateContext();
        context.Events.AddRange(
            MakeEvent(EventStatus.Approved),
            MakeEvent(EventStatus.Published)
        );
        await context.SaveChangesAsync();

        var service = new EventReviewService(context, new FakeImageStorage(), null!);
        var result = await service.GetPendingEventsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPendingEventsAsync_ResolvesPosterImageUrl()
    {
        using var context = CreateContext();
        var pendingWithPoster = MakeEvent(EventStatus.Pending, "With Poster", null, "events/sample.jpg");
        context.Events.Add(pendingWithPoster);
        await context.SaveChangesAsync();

        var service = new EventReviewService(context, new FakeImageStorage(), null!);
        var result = await service.GetPendingEventsAsync();

        Assert.Single(result);
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/event-posters/events/sample.jpg", result[0].ImageUrl);
    }

    // =========================================================================
    // GetPendingEventByIdAsync Tests
    // =========================================================================

    [Fact]
    public async Task GetPendingEventByIdAsync_WithPendingEvent_ReturnsReviewDto()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Pending Event");
        context.Events.Add(pending);
        await context.SaveChangesAsync();

        var service = new EventReviewService(context, new FakeImageStorage(), null!);
        var result = await service.GetPendingEventByIdAsync(pending.Id);

        Assert.NotNull(result);
        Assert.Equal(pending.Id, result.Id);
        Assert.Equal(pending.Title, result.Title);
        Assert.Equal(pending.Description, result.Description);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task GetPendingEventByIdAsync_WithNonPendingEvent_ReturnsNull()
    {
        using var context = CreateContext();
        var approved = MakeEvent(EventStatus.Approved);
        var rejected = MakeEvent(EventStatus.Rejected);
        var published = MakeEvent(EventStatus.Published);
        context.Events.AddRange(approved, rejected, published);
        await context.SaveChangesAsync();

        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        Assert.Null(await service.GetPendingEventByIdAsync(approved.Id));
        Assert.Null(await service.GetPendingEventByIdAsync(rejected.Id));
        Assert.Null(await service.GetPendingEventByIdAsync(published.Id));
    }

    [Fact]
    public async Task GetPendingEventByIdAsync_WithNonExistentId_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);
        var result = await service.GetPendingEventByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // =========================================================================
    // ApproveEventAsync Tests (Pending -> Approved)
    // =========================================================================

    [Fact]
    public async Task ApproveEventAsync_WhenPending_TransitionsToApprovedAndSetsMetadata()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Awaiting Approval");
        context.Events.Add(pending);
        await context.SaveChangesAsync();

        var reviewerId = Guid.NewGuid();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.ApproveEventAsync(pending.Id, reviewerId);

        Assert.False(isNotFound);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.Equal(reviewerId, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);

        // Verify persisted state in DB
        var dbItem = await context.Events.FindAsync(pending.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(EventStatus.Approved, dbItem.Status);
        Assert.Equal(reviewerId, dbItem.ReviewedBy);
        Assert.NotNull(dbItem.ReviewedAt);
    }

    [Theory]
    [InlineData(EventStatus.Approved)]
    [InlineData(EventStatus.Rejected)]
    [InlineData(EventStatus.Published)]
    public async Task ApproveEventAsync_WhenNotPending_ReturnsConflict(EventStatus initialStatus)
    {
        using var context = CreateContext();
        var evt = MakeEvent(initialStatus, "Non-Pending Event");
        context.Events.Add(evt);
        await context.SaveChangesAsync();

        var reviewerId = Guid.NewGuid();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.ApproveEventAsync(evt.Id, reviewerId);

        Assert.False(isNotFound);
        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Contains("Only Pending events can be approved", error);

        // DB state unchanged
        var dbItem = await context.Events.FindAsync(evt.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(initialStatus, dbItem.Status);
    }

    [Fact]
    public async Task ApproveEventAsync_WhenNotFound_ReturnsIsNotFoundTrue()
    {
        using var context = CreateContext();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.ApproveEventAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(isNotFound);
        Assert.Null(result);
    }

    // =========================================================================
    // RejectEventAsync Tests (Pending -> Rejected)
    // =========================================================================

    [Fact]
    public async Task RejectEventAsync_WhenPending_TransitionsToRejectedAndSetsMetadata()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Awaiting Rejection");
        context.Events.Add(pending);
        await context.SaveChangesAsync();

        var reviewerId = Guid.NewGuid();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.RejectEventAsync(pending.Id, reviewerId, "Missing details");

        Assert.False(isNotFound);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal(reviewerId, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);

        // Verify persisted state in DB
        var dbItem = await context.Events.FindAsync(pending.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(EventStatus.Rejected, dbItem.Status);
        Assert.Equal(reviewerId, dbItem.ReviewedBy);
        Assert.NotNull(dbItem.ReviewedAt);
    }

    [Theory]
    [InlineData(EventStatus.Approved)]
    [InlineData(EventStatus.Rejected)]
    [InlineData(EventStatus.Published)]
    public async Task RejectEventAsync_WhenNotPending_ReturnsConflict(EventStatus initialStatus)
    {
        using var context = CreateContext();
        var evt = MakeEvent(initialStatus, "Non-Pending Event");
        context.Events.Add(evt);
        await context.SaveChangesAsync();

        var reviewerId = Guid.NewGuid();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.RejectEventAsync(evt.Id, reviewerId, "Reason");

        Assert.False(isNotFound);
        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Contains("Only Pending events can be rejected", error);

        // DB state unchanged
        var dbItem = await context.Events.FindAsync(evt.Id);
        Assert.NotNull(dbItem);
        Assert.Equal(initialStatus, dbItem.Status);
    }

    [Fact]
    public async Task RejectEventAsync_WhenNotFound_ReturnsIsNotFoundTrue()
    {
        using var context = CreateContext();
        var service = new EventReviewService(context, new FakeImageStorage(), null!);

        var (result, error, isNotFound) = await service.RejectEventAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(isNotFound);
        Assert.Null(result);
    }

    // =========================================================================
    // EventsController Integration Tests with Controller Actions
    // =========================================================================

    private static EventsController CreateControllerWithReviewService(EventDbContext context)
    {
        var imageStorage = new FakeImageStorage();
        var reviewService = new EventReviewService(context, imageStorage, null!);
        var controller = new EventsController(context, reviewService: reviewService);

        var adminId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim("sub", adminId.ToString()),
            new Claim(ClaimTypes.Role, "Administrator"),
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public async Task Controller_GetPendingEvents_ReturnsOkWithPendingList()
    {
        using var context = CreateContext();
        context.Events.AddRange(
            MakeEvent(EventStatus.Pending, "Pending 1"),
            MakeEvent(EventStatus.Approved, "Approved 1")
        );
        await context.SaveChangesAsync();

        var controller = CreateControllerWithReviewService(context);
        var actionResult = await controller.GetPendingEvents(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AdminEventReviewDto>>(okResult.Value);
        Assert.Single(list);
        Assert.Equal("Pending 1", list[0].Title);
    }

    [Fact]
    public async Task Controller_GetPendingEventById_Returns200ForPending_404ForOthers()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Pending Event");
        var approved = MakeEvent(EventStatus.Approved, "Approved Event");
        context.Events.AddRange(pending, approved);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithReviewService(context);

        var okResult = await controller.GetPendingEventById(pending.Id.ToString(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(okResult.Result);

        var notFoundForApproved = await controller.GetPendingEventById(approved.Id.ToString(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(notFoundForApproved.Result);

        var notFoundForRandom = await controller.GetPendingEventById(Guid.NewGuid().ToString(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(notFoundForRandom.Result);
    }

    [Fact]
    public async Task Controller_ApproveEvent_ReturnsOkAndUpdatedDto()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Pending To Approve");
        context.Events.Add(pending);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithReviewService(context);
        var actionResult = await controller.ApproveEvent(pending.Id.ToString(), new ReviewEventRequest());

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<AdminEventReviewDto>(okResult.Value);
        Assert.Equal("Approved", dto.Status);
        Assert.NotNull(dto.ReviewedAt);
    }

    [Fact]
    public async Task Controller_ApproveEvent_WhenAlreadyApproved_Returns409Conflict()
    {
        using var context = CreateContext();
        var approved = MakeEvent(EventStatus.Approved, "Already Approved");
        context.Events.Add(approved);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithReviewService(context);
        var actionResult = await controller.ApproveEvent(approved.Id.ToString(), new ReviewEventRequest());

        Assert.IsType<ConflictObjectResult>(actionResult);
    }

    [Fact]
    public async Task Controller_RejectEvent_ReturnsOkAndUpdatedDto()
    {
        using var context = CreateContext();
        var pending = MakeEvent(EventStatus.Pending, "Pending To Reject");
        context.Events.Add(pending);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithReviewService(context);
        var actionResult = await controller.RejectEvent(pending.Id.ToString(), new ReviewEventRequest { Notes = "Spam event" });

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<AdminEventReviewDto>(okResult.Value);
        Assert.Equal("Rejected", dto.Status);
        Assert.NotNull(dto.ReviewedAt);
    }
}
