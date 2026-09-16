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

    // =========================================================================
    // EP-38 / US-18: Filter Events Tests
    // =========================================================================

    [Fact]
    public async Task GetEvents_WithCategoryFilter_ReturnsMatchingEventsOnly()
    {
        var music = MakeEvent(EventStatus.Published, "Rock Night");
        music.Category = "Music";
        var conference = MakeEvent(EventStatus.Published, "Tech Conf");
        conference.Category = "Conference";

        using var context = CreateContextWithEvents(music, conference);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Category = "Music" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Rock Night", dtos[0].Title);
        Assert.Equal("Music", dtos[0].Category);
    }

    [Fact]
    public async Task GetEvents_WithCategoryFilter_NormalizedAlias_ReturnsCanonicalEvents()
    {
        var music = MakeEvent(EventStatus.Published, "Acoustic Session");
        music.Category = "Music";

        using var context = CreateContextWithEvents(music);
        var controller = CreateController(context);

        // Alias "Musical Concert" normalizes to "Music"
        var result = await controller.GetEvents(new EventDiscoveryQuery { Category = "Musical Concert" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Acoustic Session", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithVenueTypeFilter_ReturnsMatchingEventsOnly()
    {
        var indoor = MakeEvent(EventStatus.Published, "Indoor Concert");
        indoor.VenueType = "Indoor";
        var outdoor = MakeEvent(EventStatus.Published, "Open Air Fest");
        outdoor.VenueType = "Outdoor";

        using var context = CreateContextWithEvents(indoor, outdoor);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { VenueType = "Outdoor" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Open Air Fest", dtos[0].Title);
        Assert.Equal("Outdoor", dtos[0].VenueType);
    }

    [Fact]
    public async Task GetEvents_WithDateFilter_Today_ReturnsEventsHappeningToday()
    {
        var todayEvent = MakeEvent(EventStatus.Published, "Today Show");
        todayEvent.EventDate = DateTime.UtcNow.Date.AddHours(14); // Today at 14:00 UTC
        var nextMonthEvent = MakeEvent(EventStatus.Published, "Future Show");
        nextMonthEvent.EventDate = DateTime.UtcNow.Date.AddDays(40);

        using var context = CreateContextWithEvents(todayEvent, nextMonthEvent);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Date = "today" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Today Show", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithDateFilter_ThisWeek_ReturnsEventsWithinCurrentWeek()
    {
        var now = DateTime.UtcNow;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var startOfWeek = todayStart.AddDays(-diff);

        var thisWeekEvent = MakeEvent(EventStatus.Published, "Midweek Event");
        thisWeekEvent.EventDate = startOfWeek.AddDays(3).AddHours(10); // Thursday of this week
        var farEvent = MakeEvent(EventStatus.Published, "Far Future Event");
        farEvent.EventDate = startOfWeek.AddDays(25);

        using var context = CreateContextWithEvents(thisWeekEvent, farEvent);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Date = "this-week" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Midweek Event", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithDateFilter_ThisMonth_ReturnsEventsWithinCurrentMonth()
    {
        var now = DateTime.UtcNow;
        var thisMonthEvent = MakeEvent(EventStatus.Published, "Month Gala");
        thisMonthEvent.EventDate = new DateTime(now.Year, now.Month, 15, 12, 0, 0, DateTimeKind.Utc);
        var nextYearEvent = MakeEvent(EventStatus.Published, "Next Year Gala");
        nextYearEvent.EventDate = thisMonthEvent.EventDate.AddYears(1);

        using var context = CreateContextWithEvents(thisMonthEvent, nextYearEvent);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Date = "this-month" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Month Gala", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithSearchAndCategory_CombinesBothFilters()
    {
        var match = MakeEvent(EventStatus.Published, "Rock Fest 2026");
        match.Category = "Music";

        var wrongCat = MakeEvent(EventStatus.Published, "Rock Climb Challenge");
        wrongCat.Category = "Sports";

        var wrongSearch = MakeEvent(EventStatus.Published, "Jazz Fest");
        wrongSearch.Category = "Music";

        using var context = CreateContextWithEvents(match, wrongCat, wrongSearch);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery
        {
            Search = "Rock",
            Category = "Music"
        });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Rock Fest 2026", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithSearchAndVenueType_CombinesBothFilters()
    {
        var match = MakeEvent(EventStatus.Published, "Sunset Acoustic");
        match.VenueType = "Outdoor";

        var indoor = MakeEvent(EventStatus.Published, "Sunset Arena");
        indoor.VenueType = "Indoor";

        using var context = CreateContextWithEvents(match, indoor);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery
        {
            Search = "Sunset",
            VenueType = "Outdoor"
        });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Sunset Acoustic", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithCategoryAndVenueType_CombinesBothFilters()
    {
        var outdoorMusic = MakeEvent(EventStatus.Published, "Garden Symphony");
        outdoorMusic.Category = "Music";
        outdoorMusic.VenueType = "Outdoor";

        var indoorMusic = MakeEvent(EventStatus.Published, "Hall Symphony");
        indoorMusic.Category = "Music";
        indoorMusic.VenueType = "Indoor";

        var outdoorSports = MakeEvent(EventStatus.Published, "Garden Marathon");
        outdoorSports.Category = "Sports";
        outdoorSports.VenueType = "Outdoor";

        using var context = CreateContextWithEvents(outdoorMusic, indoorMusic, outdoorSports);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery
        {
            Category = "Music",
            VenueType = "Outdoor"
        });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Garden Symphony", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithSearchCategoryVenueTypeAndDate_CombinesAllFilters()
    {
        var now = DateTime.UtcNow;
        var match = MakeEvent(EventStatus.Published, "Lakeside Acoustic");
        match.Category = "Music";
        match.VenueType = "Outdoor";
        match.EventDate = new DateTime(now.Year, now.Month, 10, 15, 0, 0, DateTimeKind.Utc);

        var wrongVenue = MakeEvent(EventStatus.Published, "Lakeside Acoustic Indoor");
        wrongVenue.Category = "Music";
        wrongVenue.VenueType = "Indoor";
        wrongVenue.EventDate = match.EventDate;

        var wrongDate = MakeEvent(EventStatus.Published, "Lakeside Acoustic Future");
        wrongDate.Category = "Music";
        wrongDate.VenueType = "Outdoor";
        wrongDate.EventDate = match.EventDate.AddMonths(2);

        using var context = CreateContextWithEvents(match, wrongVenue, wrongDate);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery
        {
            Search = "Lakeside",
            Category = "Music",
            VenueType = "Outdoor",
            Date = "this-month"
        });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal("Lakeside Acoustic", dtos[0].Title);
    }

    [Fact]
    public async Task GetEvents_WithInvalidFilterValues_ReturnsEmptyListSafely()
    {
        var ev = MakeEvent(EventStatus.Published, "Real Event");
        ev.Category = "Music";
        ev.VenueType = "Outdoor";

        using var context = CreateContextWithEvents(ev);
        var controller = CreateController(context);

        var resultInvalidVenue = await controller.GetEvents(new EventDiscoveryQuery { VenueType = "Space" });
        var okVenue = Assert.IsType<OkObjectResult>(resultInvalidVenue.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okVenue.Value));

        var resultInvalidDate = await controller.GetEvents(new EventDiscoveryQuery { Date = "banana" });
        var okDate = Assert.IsType<OkObjectResult>(resultInvalidDate.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okDate.Value));

        var resultInvalidCat = await controller.GetEvents(new EventDiscoveryQuery { Category = "NonExistentCategory" });
        var okCat = Assert.IsType<OkObjectResult>(resultInvalidCat.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okCat.Value));
    }

    [Fact]
    public async Task GetEvents_WithFilters_EnforcesCustomerVisibilityExcludingPendingAndCancelled()
    {
        var publishedMusic = MakeEvent(EventStatus.Published, "Published Music");
        publishedMusic.Category = "Music";
        var pendingMusic = MakeEvent(EventStatus.Pending, "Pending Music");
        pendingMusic.Category = "Music";
        var cancelledMusic = MakeEvent(EventStatus.Cancelled, "Cancelled Music");
        cancelledMusic.Category = "Music";

        using var context = CreateContextWithEvents(publishedMusic, pendingMusic, cancelledMusic);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery { Category = "Music" });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Single(dtos);
        Assert.Equal(publishedMusic.Id, dtos[0].Id);
    }

    [Fact]
    public async Task GetEvents_WithNoFilters_ReturnsAllPublishedEvents()
    {
        var ev1 = MakeEvent(EventStatus.Published, "Event 1");
        var ev2 = MakeEvent(EventStatus.Published, "Event 2");

        using var context = CreateContextWithEvents(ev1, ev2);
        var controller = CreateController(context);

        var result = await controller.GetEvents(new EventDiscoveryQuery());
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value).ToList();

        Assert.Equal(2, dtos.Count);
    }
}
