using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventsControllerTests
{
    private static EventDbContext CreateContextWithEvents(params Event[] events)
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EventDbContext(options);
        context.Events.AddRange(events);
        context.SaveChanges();
        return context;
    }

    private static EventsController CreateController(EventDbContext context)
    {
        return new EventsController(context, null);
    }

    private static Event MakeEvent(EventStatus status, string title = "Test Event") => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = "Description",
        Venue = "Venue",
        EventDate = DateTime.UtcNow.AddDays(10),
        Price = 1000m,
        Status = status,
        OrganizerId = Guid.NewGuid()
    };

    // ---------- GetEvents (US-08) ----------

    [Fact]
    public async Task GetEvents_ReturnsOnlyPublishedEvents_ExcludesAllOtherStatuses()
    {
        // Approve now transitions Pending directly to Published (no separate
        // Approved holding state), so only Published events should ever be
        // visible on the public homepage. Approved is included here as a
        // status that must NOT leak through, alongside Pending and Rejected.
        var pending = MakeEvent(EventStatus.Pending, "Pending Event");
        var approved = MakeEvent(EventStatus.Approved, "Approved Event");
        var rejected = MakeEvent(EventStatus.Rejected, "Rejected Event");
        var published = MakeEvent(EventStatus.Published, "Published Event");

        using var context = CreateContextWithEvents(pending, approved, rejected, published);
        var controller = CreateController(context);

        var result = await controller.GetEvents();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value);

        Assert.Single(returned);
        Assert.DoesNotContain(returned, e => e.Status == EventStatus.Pending);
        Assert.DoesNotContain(returned, e => e.Status == EventStatus.Rejected);
        Assert.DoesNotContain(returned, e => e.Status == EventStatus.Approved);
        Assert.Contains(returned, e => e.Status == EventStatus.Published);
    }

    [Fact]
    public async Task GetEvents_WithNoEvents_ReturnsEmptyArrayNot404()
    {
        using var context = CreateContextWithEvents();
        var controller = CreateController(context);

        var result = await controller.GetEvents();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value);

        Assert.Empty(returned);
    }

    private class FakeImageStorage : Storage.IEventImageStorage
    {
        public Task<string> UploadAsync(Stream imageStream, string contentType, string originalFileName, string folderPrefix = "event-posters", CancellationToken cancellationToken = default)
            => Task.FromResult($"{folderPrefix}/fake.webp");
        public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public string? GetPublicUrl(string? blobName)
            => string.IsNullOrEmpty(blobName) ? null : $"http://127.0.0.1:10000/devstoreaccount1/event-posters/{blobName}";
    }

    [Fact]
    public async Task GetEvents_WithPosterImage_PopulatesImageUrl()
    {
        // Seeded as Published (not Approved) since only Published events
        // are returned by GetEvents after the Approve+Publish merge.
        var publishedWithImage = MakeEvent(EventStatus.Published, "Published With Image");
        publishedWithImage.ImageBlobName = "events/test.jpg";
        var publishedWithoutImage = MakeEvent(EventStatus.Published, "Published Without Image");

        using var context = CreateContextWithEvents(publishedWithImage, publishedWithoutImage);
        var controller = new EventsController(context, imageStorage: new FakeImageStorage());

        var result = await controller.GetEvents();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        var withImage = returned.First(e => e.Title == "Published With Image");
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/event-posters/events/test.jpg", withImage.ImageUrl);

        var withoutImage = returned.First(e => e.Title == "Published Without Image");
        Assert.Null(withoutImage.ImageUrl);
    }

    [Fact]
    public async Task GetEventById_WithPosterImage_PopulatesImageUrl()
    {
        // Seeded as Published (not Approved) since only Published events
        // are resolvable via the public detail endpoint after the merge.
        var published = MakeEvent(EventStatus.Published, "Published Event");
        published.ImageBlobName = "events/detail.png";
        using var context = CreateContextWithEvents(published);
        var controller = new EventsController(context, imageStorage: new FakeImageStorage());

        var result = await controller.GetEventById(published.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EventDetailsDto>(okResult.Value);
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/event-posters/events/detail.png", dto.ImageUrl);
    }

    // ---------- GetEventById (US-09) ----------

    [Fact]
    public async Task GetEventById_WithApprovedEvent_Returns404()
    {
        // Approved is a review-workflow status, not a public visibility
        // status — only Published events should be resolvable via the
        // public detail endpoint. Mirrors the Pending/Rejected 404 tests below.
        var approved = MakeEvent(EventStatus.Approved, "Approved Event");
        using var context = CreateContextWithEvents(approved);
        var controller = CreateController(context);

        var result = await controller.GetEventById(approved.Id.ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithPublishedEvent_ReturnsEventDetails()
    {
        var published = MakeEvent(EventStatus.Published, "Published Event");
        using var context = CreateContextWithEvents(published);
        var controller = CreateController(context);

        var result = await controller.GetEventById(published.Id.ToString());

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EventDetailsDto>(okResult.Value);
        Assert.Equal(published.Id, dto.Id);
    }

    [Fact]
    public async Task GetEventById_WithPendingEvent_Returns404()
    {
        var pending = MakeEvent(EventStatus.Pending);
        using var context = CreateContextWithEvents(pending);
        var controller = CreateController(context);

        var result = await controller.GetEventById(pending.Id.ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithRejectedEvent_Returns404()
    {
        var rejected = MakeEvent(EventStatus.Rejected);
        using var context = CreateContextWithEvents(rejected);
        var controller = CreateController(context);

        var result = await controller.GetEventById(rejected.Id.ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithNonExistentId_Returns404()
    {
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = CreateController(context);

        var result = await controller.GetEventById(Guid.NewGuid().ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithMalformedId_Returns404NotServerError()
    {
        // See TC-EVT-008 discrepancy note: matrix expects 400, code returns 404.
        // This test documents ACTUAL behavior — flag the mismatch separately, don't silently "fix" the test to hide it.
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = CreateController(context);

        var result = await controller.GetEventById("not-a-guid");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithSqlInjectionStyleId_Returns404NotServerError()
    {
        // TC-EVT-011 — Critical priority in your matrix
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = CreateController(context);

        var result = await controller.GetEventById("' OR '1'='1");

        Assert.IsType<NotFoundResult>(result.Result);
    }
}