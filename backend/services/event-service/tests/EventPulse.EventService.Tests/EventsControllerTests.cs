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
    public async Task GetEvents_ReturnsAllEvents_RegardlessOfStatus_DocumentsCurrentGap()
    {
        // This test documents CURRENT behavior per the controller's own comment:
        // "Returns all events for EP-103 foundation. Published filtering will be added in EP-104."
        // Update this test (don't just re-run it) once EP-104 filtering lands —
        // correct US-08 behavior should EXCLUDE Pending/Rejected events.
        var pending = MakeEvent(EventStatus.Pending, "Pending Event");
        var approved = MakeEvent(EventStatus.Approved, "Approved Event");
        var rejected = MakeEvent(EventStatus.Rejected, "Rejected Event");
        var published = MakeEvent(EventStatus.Published, "Published Event");

        using var context = CreateContextWithEvents(pending, approved, rejected, published);
        var controller = new EventsController(context);

        var result = await controller.GetEvents();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value);

        Assert.Equal(4, returned.Count()); // includes Pending/Rejected — the current gap, not a false assumption
    }

    [Fact]
    public async Task GetEvents_WithNoEvents_ReturnsEmptyArrayNot404()
    {
        using var context = CreateContextWithEvents();
        var controller = new EventsController(context);

        var result = await controller.GetEvents();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okResult.Value);

        Assert.Empty(returned);
    }

    // ---------- GetEventById (US-09) ----------

    [Fact]
    public async Task GetEventById_WithApprovedEvent_ReturnsEventDetails()
    {
        var approved = MakeEvent(EventStatus.Approved, "Approved Event");
        using var context = CreateContextWithEvents(approved);
        var controller = new EventsController(context);

        var result = await controller.GetEventById(approved.Id.ToString());

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EventDetailsDto>(okResult.Value);
        Assert.Equal(approved.Id, dto.Id);
        Assert.Equal(approved.Title, dto.Title);
    }

    [Fact]
    public async Task GetEventById_WithPublishedEvent_ReturnsEventDetails()
    {
        var published = MakeEvent(EventStatus.Published, "Published Event");
        using var context = CreateContextWithEvents(published);
        var controller = new EventsController(context);

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
        var controller = new EventsController(context);

        var result = await controller.GetEventById(pending.Id.ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithRejectedEvent_Returns404()
    {
        var rejected = MakeEvent(EventStatus.Rejected);
        using var context = CreateContextWithEvents(rejected);
        var controller = new EventsController(context);

        var result = await controller.GetEventById(rejected.Id.ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithNonExistentId_Returns404()
    {
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = new EventsController(context);

        var result = await controller.GetEventById(Guid.NewGuid().ToString());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithMalformedId_Returns404NotServerError()
    {
        // See TC-EVT-008 discrepancy note: matrix expects 400, code returns 404.
        // This test documents ACTUAL behavior — flag the mismatch separately, don't silently "fix" the test to hide it.
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = new EventsController(context);

        var result = await controller.GetEventById("not-a-guid");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetEventById_WithSqlInjectionStyleId_Returns404NotServerError()
    {
        // TC-EVT-011 — Critical priority in your matrix
        using var context = CreateContextWithEvents(MakeEvent(EventStatus.Approved));
        var controller = new EventsController(context);

        var result = await controller.GetEventById("' OR '1'='1");

        Assert.IsType<NotFoundResult>(result.Result);
    }
}