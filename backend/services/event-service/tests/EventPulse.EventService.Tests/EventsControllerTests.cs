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

    // =========================================================================
    // EP-37 / US-17 — SEARCH & AUTOCOMPLETE TESTS
    // =========================================================================

    [Fact]
    public async Task GetEvents_WithSearchMatchingTitle_ReturnsMatchingPublishedEvents()
    {
        var ev1 = MakeEvent(EventStatus.Published, "Sensation Live in Concert");
        var ev2 = MakeEvent(EventStatus.Published, "Tech Future Summit");

        using var context = CreateContextWithEvents(ev1, ev2);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Search = "sensation" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal(ev1.Id, dtos[0].Id);
    }

    [Fact]
    public async Task GetEvents_WithSearchMatchingVenue_ReturnsMatchingPublishedEvents()
    {
        var ev1 = MakeEvent(EventStatus.Published, "Grand Gala");
        ev1.Venue = "Nelum Pokuna Theatre";

        var ev2 = MakeEvent(EventStatus.Published, "Cricket Match");
        ev2.Venue = "R. Premadasa Stadium";

        using var context = CreateContextWithEvents(ev1, ev2);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Search = "pokuna" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal(ev1.Id, dtos[0].Id);
    }

    [Fact]
    public async Task GetEvents_WithSearchMatchingCategoryAndAlias_ReturnsMatchingEvents()
    {
        var ev1 = MakeEvent(EventStatus.Published, "Melody Night");
        ev1.Category = "Music";

        var ev2 = MakeEvent(EventStatus.Published, "Badminton Cup");
        ev2.Category = "Sports";

        using var context = CreateContextWithEvents(ev1, ev2);
        var controller = CreateController(context);

        // Search canonical category "Music"
        var res1 = await controller.GetEvents(new EventDiscoveryQuery { Search = "music" });
        var ok1 = Assert.IsType<OkObjectResult>(res1.Result);
        var dtos1 = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(ok1.Value).ToList();
        Assert.Single(dtos1);
        Assert.Equal(ev1.Id, dtos1[0].Id);

        // Search legacy alias "Musical Concert" which normalizes to "Music"
        var res2 = await controller.GetEvents(new EventDiscoveryQuery { Search = "Musical Concert" });
        var ok2 = Assert.IsType<OkObjectResult>(res2.Result);
        var dtos2 = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(ok2.Value).ToList();
        Assert.Single(dtos2);
        Assert.Equal(ev1.Id, dtos2[0].Id);
    }

    [Fact]
    public async Task GetEvents_WithSearch_ExcludesNonPublishedEventsEvenIfTitleMatches()
    {
        var published = MakeEvent(EventStatus.Published, "Rock Fest Live");
        var pending = MakeEvent(EventStatus.Pending, "Rock Fest Live");
        var approved = MakeEvent(EventStatus.Approved, "Rock Fest Live");
        var rejected = MakeEvent(EventStatus.Rejected, "Rock Fest Live");
        var cancelled = MakeEvent(EventStatus.Cancelled, "Rock Fest Live");

        using var context = CreateContextWithEvents(published, pending, approved, rejected, cancelled);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Search = "Rock" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal(published.Id, dtos[0].Id);
        Assert.Equal(EventStatus.Published, dtos[0].Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData(" s ")]
    public async Task GetSuggestions_WithShorterThan2Chars_ReturnsEmptyListImmediately(string? query)
    {
        var ev = MakeEvent(EventStatus.Published, "Sensation Live");
        using var context = CreateContextWithEvents(ev);
        var controller = CreateController(context);

        var result = await controller.GetSuggestions(query);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<EventSuggestionDto>>(okResult.Value).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetSuggestions_WithValidQuery_ReturnsAtMost5LightweightItemsOrderedByDate()
    {
        var events = new System.Collections.Generic.List<EventPulse.EventService.Models.Event>();
        for (int i = 0; i < 8; i++)
        {
            var e = MakeEvent(EventStatus.Published, $"Festival Day {i + 1}");
            e.Category = "Festival";
            e.Venue = $"Venue {i}";
            e.EventDate = System.DateTime.UtcNow.AddDays(i + 1);
            events.Add(e);
        }

        using var context = CreateContextWithEvents(events.ToArray());
        var controller = CreateController(context);

        var result = await controller.GetSuggestions("Festival");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var suggestions = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<EventSuggestionDto>>(okResult.Value).ToList();

        Assert.Equal(5, suggestions.Count);
        Assert.Equal("Festival Day 1", suggestions[0].Title);
        Assert.Equal("Festival Day 5", suggestions[4].Title);
    }

    [Fact]
    public async Task GetSuggestions_ExcludesNonPublishedEvents()
    {
        var published = MakeEvent(EventStatus.Published, "Jazz Evening");
        var pending = MakeEvent(EventStatus.Pending, "Jazz Afternoon");
        var cancelled = MakeEvent(EventStatus.Cancelled, "Jazz Night");

        using var context = CreateContextWithEvents(published, pending, cancelled);
        var controller = CreateController(context);

        var result = await controller.GetSuggestions("Jazz");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var suggestions = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<EventSuggestionDto>>(okResult.Value).ToList();

        Assert.Single(suggestions);
        Assert.Equal(published.Id, suggestions[0].Id);
    }

    [Fact]
    public async Task GetSuggestions_WithPosterImage_PopulatesImageUrl()
    {
        var publishedWithImage = MakeEvent(EventStatus.Published, "Rock Concert Live");
        publishedWithImage.ImageBlobName = "posters/rock.webp";
        var publishedWithoutImage = MakeEvent(EventStatus.Published, "Rock Acoustic");

        using var context = CreateContextWithEvents(publishedWithImage, publishedWithoutImage);
        var controller = new EventsController(context, imageStorage: new FakeImageStorage());

        var result = await controller.GetSuggestions("Rock");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var suggestions = Assert.IsAssignableFrom<IEnumerable<EventSuggestionDto>>(okResult.Value).ToList();

        var withImage = suggestions.First(e => e.Title == "Rock Concert Live");
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/event-posters/posters/rock.webp", withImage.ImageUrl);

        var withoutImage = suggestions.First(e => e.Title == "Rock Acoustic");
        Assert.Null(withoutImage.ImageUrl);
    }
}
