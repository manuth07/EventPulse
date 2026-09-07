using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Data;

public static class EventDbSeeder
{
    public static async Task SeedAsync(EventDbContext context)
    {
        if (await context.Events.AnyAsync())
        {
            // Backfill existing seed events that have null Category/VenueType
            var unpopulated = await context.Events.Where(e => e.Category == null).ToListAsync();
            if (unpopulated.Any())
            {
                foreach (var ev in unpopulated)
                {
                    if (ev.Id == Guid.Parse("a1111111-1111-1111-1111-111111111111"))
                    {
                        ev.Category = "Conference";
                        ev.VenueType = "Indoor";
                    }
                    else if (ev.Id == Guid.Parse("b2222222-2222-2222-2222-222222222222"))
                    {
                        ev.Category = "Musical Concert";
                        ev.VenueType = "Outdoor";
                    }
                    else if (ev.Id == Guid.Parse("c3333333-3333-3333-3333-333333333333"))
                    {
                        ev.Category = "Conference";
                        ev.VenueType = "Indoor";
                    }
                    else if (ev.Id == Guid.Parse("d4444444-4444-4444-4444-444444444444"))
                    {
                        ev.Category = "Workshop";
                        ev.VenueType = "Indoor";
                    }
                    else if (ev.Id == Guid.Parse("e5555555-5555-5555-5555-555555555555"))
                    {
                        ev.Category = "Other";
                        ev.VenueType = "Indoor";
                    }
                }
                await context.SaveChangesAsync();
            }
            return;
        }

        var organizer1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var organizer2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var adminId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var events = new List<Event>
        {
            new Event
            {
                Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                Title = "Tech Conference 2026",
                Description = "Annual flagship technology and software engineering conference in Sri Lanka.",
                Venue = "BMICH, Colombo",
                EventDate = DateTime.UtcNow.AddDays(30),
                Price = 5000.00m,
                Category = "Conference",
                VenueType = "Indoor",
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ReviewedAt = DateTime.UtcNow.AddDays(-5),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                Title = "Colombo Music Festival",
                Description = "Outdoor live music performance featuring top national and international artists.",
                Venue = "Galle Face Green, Colombo",
                EventDate = DateTime.UtcNow.AddDays(45),
                Price = 3500.00m,
                Category = "Musical Concert",
                VenueType = "Outdoor",
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                ReviewedAt = DateTime.UtcNow.AddDays(-4),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                Title = "Startup Meetup 2026",
                Description = "Networking and pitch event for early stage Sri Lankan tech startups.",
                Venue = "Trace Expert City, Colombo 10",
                EventDate = DateTime.UtcNow.AddDays(15),
                Price = 0.00m,
                Category = "Conference",
                VenueType = "Indoor",
                Status = EventStatus.Pending,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ReviewedAt = null,
                ReviewedBy = null
            },
            new Event
            {
                Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                Title = "AI Workshop Sri Lanka",
                Description = "Hands-on workshop covering LLMs, Agentic AI systems, and machine learning deployment.",
                Venue = "SLIIT Auditorium, Malabe",
                EventDate = DateTime.UtcNow.AddDays(60),
                Price = 2500.00m,
                Category = "Workshop",
                VenueType = "Indoor",
                Status = EventStatus.Approved,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                ReviewedAt = DateTime.UtcNow.AddDays(-1),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"),
                Title = "Rejected Test Event",
                Description = "Sample event that failed verification standards and was rejected by admin.",
                Venue = "Virtual / Online",
                EventDate = DateTime.UtcNow.AddDays(10),
                Price = 1000.00m,
                Category = "Other",
                VenueType = "Indoor",
                Status = EventStatus.Rejected,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                ReviewedAt = DateTime.UtcNow.AddDays(-11),
                ReviewedBy = adminId,
                ReviewComment = "The venue address is incomplete. Please provide the full physical venue address and detailed event schedule."
            }
        };

        await context.Events.AddRangeAsync(events);
        await context.SaveChangesAsync();
    }
}
