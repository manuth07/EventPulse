using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;

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
}
