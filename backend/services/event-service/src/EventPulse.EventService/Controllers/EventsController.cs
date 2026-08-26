using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly EventDbContext _context;

    public EventsController(EventDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/events
    /// Retrieves event records from the database.
    /// Note: Returns all events for EP-103 foundation. Published filtering will be added in EP-104.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventListDto>>> GetEvents()
    {
        var events = await _context.Events
            .AsNoTracking()
            .Select(e => new EventListDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Venue = e.Venue,
                EventDate = e.EventDate,
                Price = e.Price,
                Status = e.Status
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// GET /api/events/{id}
    /// Retrieves a single published or approved event by ID for public visitors.
    /// Returns 404 Not Found if event does not exist, is invalid, or is non-public.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EventDetailsDto>> GetEventById(string id)
    {
        if (!Guid.TryParse(id, out var guidId))
        {
            return NotFound();
        }

        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == guidId);

        if (eventItem == null)
        {
            return NotFound();
        }

        // Only Published (4) or Approved (2) events are visible to public visitors
        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.Approved)
        {
            return NotFound();
        }

        var details = new EventDetailsDto
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            Venue = eventItem.Venue,
            EventDate = eventItem.EventDate,
            Price = eventItem.Price,
            OrganizerId = eventItem.OrganizerId
        };

        return Ok(details);
    }
}
