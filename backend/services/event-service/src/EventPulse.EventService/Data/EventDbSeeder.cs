using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Data;

public static class EventDbSeeder
{
    public static async Task SeedAsync(EventDbContext context)
    {
        var organizer1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var organizer2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var adminId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var seedEvents = new List<Event>
        {
            new Event
            {
                Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                Title = "Tech Conference 2026",
                Description = "Annual flagship technology and software engineering conference in Sri Lanka.",
                Venue = "BMICH, Colombo",
                EventDate = DateTime.UtcNow.AddDays(45),
                Price = 7500.00m,
                Category = "Conference",
                VenueType = "Indoor",
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ReviewedAt = DateTime.UtcNow.AddDays(-5),
                ReviewedBy = adminId,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                        Name = "General Admission",
                        Price = 7500.00m,
                        Capacity = 100,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("a1111111-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                        Name = "VIP Pass",
                        Price = 15000.00m,
                        Capacity = 50,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    }
                }
            },
            new Event
            {
                Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                Title = "Colombo Music Festival",
                Description = "Outdoor live music performance featuring top national and international artists.",
                Venue = "Galle Face Green, Colombo",
                EventDate = DateTime.UtcNow.AddDays(60),
                Price = 5000.00m,
                Category = "Music",
                VenueType = "Outdoor",
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                ReviewedAt = DateTime.UtcNow.AddDays(-4),
                ReviewedBy = adminId,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("b2222222-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                        Name = "General Admission",
                        Price = 5000.00m,
                        Capacity = 200,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-8)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("b2222222-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                        Name = "VIP Pass",
                        Price = 12000.00m,
                        Capacity = 50,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-8)
                    }
                }
            },
            new Event
            {
                Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                Title = "Startup Meetup 2026",
                Description = "Networking and pitch event for early stage Sri Lankan tech startups.",
                Venue = "Trace Expert City, Colombo 10",
                EventDate = DateTime.UtcNow.AddDays(30),
                Price = 0.00m,
                Category = "Conference",
                VenueType = "Indoor",
                Status = EventStatus.Pending,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ReviewedAt = null,
                ReviewedBy = null,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("c3333333-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                        Name = "General Admission",
                        Price = 0.00m,
                        Capacity = 50,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("c3333333-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                        Name = "VIP Pass",
                        Price = 0.00m,
                        Capacity = 20,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    }
                }
            },
            new Event
            {
                Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                Title = "AI Workshop Sri Lanka",
                Description = "Hands-on workshop covering LLMs, Agentic AI systems, and machine learning deployment.",
                Venue = "SLIIT Auditorium, Malabe",
                EventDate = DateTime.UtcNow.AddDays(75),
                Price = 2500.00m,
                Category = "Workshop",
                VenueType = "Indoor",
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                ReviewedAt = DateTime.UtcNow.AddDays(-1),
                ReviewedBy = adminId,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("d4444444-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                        Name = "General Admission",
                        Price = 2500.00m,
                        Capacity = 80,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-6)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("d4444444-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                        Name = "VIP Pass",
                        Price = 6000.00m,
                        Capacity = 30,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-6)
                    }
                }
            },
            new Event
            {
                Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"),
                Title = "Rejected Test Event",
                Description = "Sample event that failed verification standards and was rejected by admin.",
                Venue = "Virtual / Online",
                EventDate = DateTime.UtcNow.AddDays(20),
                Price = 1000.00m,
                Category = "Other",
                VenueType = "Indoor",
                Status = EventStatus.Rejected,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                ReviewedAt = DateTime.UtcNow.AddDays(-11),
                ReviewedBy = adminId,
                ReviewComment = "The venue address is incomplete. Please provide the full physical venue address and detailed event schedule."
            },
            new Event
            {
                Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
                Title = "Cyber Security Summit Colombo",
                Description = "Sri Lanka's premier cyber security summit gathering global security leaders, ethical hackers, and CISOs.",
                Venue = "Cinnamon Grand, Colombo",
                EventDate = DateTime.UtcNow.AddDays(50),
                Price = 7500.00m,
                Category = "Conference",
                VenueType = "Indoor",
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                ReviewedAt = DateTime.UtcNow.AddDays(-10),
                ReviewedBy = adminId,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("f6666666-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
                        Name = "General Admission",
                        Price = 7500.00m,
                        Capacity = 100,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("f6666666-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
                        Name = "VIP Pass",
                        Price = 15000.00m,
                        Capacity = 50,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    }
                }
            },
            new Event
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Title = "Wellness & Beach Yoga Festival",
                Description = "A weekend of mindfulness, beach yoga sessions, meditation, and holistic health workshops.",
                Venue = "Mount Lavinia Beach Hotel",
                EventDate = DateTime.UtcNow.AddDays(70),
                Price = 4000.00m,
                Category = "Festival",
                VenueType = "Outdoor",
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                ReviewedAt = DateTime.UtcNow.AddDays(-8),
                ReviewedBy = adminId,
                TicketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        Id = Guid.Parse("77777777-0000-0000-0000-000000000001"),
                        EventId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                        Name = "General Admission",
                        Price = 4000.00m,
                        Capacity = 150,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-14)
                    },
                    new TicketType
                    {
                        Id = Guid.Parse("77777777-0000-0000-0000-000000000002"),
                        EventId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                        Name = "VIP Pass",
                        Price = 9000.00m,
                        Capacity = 40,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-14)
                    }
                }
            }
        };

        // 1. If database has no events, insert full seed data including ticket types
        if (!await context.Events.AnyAsync())
        {
            await context.Events.AddRangeAsync(seedEvents);
            await context.SaveChangesAsync();
            return;
        }

        // 2. Idempotent check: Ensure each canonical seed event exists
        foreach (var seedEvent in seedEvents)
        {
            var existingEvent = await context.Events
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == seedEvent.Id);

            if (existingEvent == null)
            {
                await context.Events.AddAsync(seedEvent);
            }
            else
            {
                // Ensure event date is in the future for local dev / booking testing
                if (existingEvent.EventDate <= DateTime.UtcNow)
                {
                    existingEvent.EventDate = seedEvent.EventDate;
                }

                // Backfill category & venue type normalization
                if (existingEvent.Category == null || existingEvent.Category == "Musical Concert" || existingEvent.Category == "Theatre / Performance")
                {
                    existingEvent.Category = seedEvent.Category;
                }
                if (string.IsNullOrEmpty(existingEvent.VenueType))
                {
                    existingEvent.VenueType = seedEvent.VenueType;
                }

                // Ensure tickets exist for this event
                if (!existingEvent.TicketTypes.Any() && seedEvent.TicketTypes.Any())
                {
                    foreach (var ticket in seedEvent.TicketTypes)
                    {
                        context.TicketTypes.Add(new TicketType
                        {
                            Id = ticket.Id,
                            EventId = existingEvent.Id,
                            Name = ticket.Name,
                            Price = ticket.Price,
                            Capacity = ticket.Capacity,
                            BookedQuantity = 0,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        // 3. Ensure any custom/other existing published or approved events in the DB also have valid ticket types
        var allEvents = await context.Events
            .Include(e => e.TicketTypes)
            .ToListAsync();

        foreach (var ev in allEvents)
        {
            // If date is in the past, update to future for local dev
            if (ev.EventDate <= DateTime.UtcNow)
            {
                ev.EventDate = DateTime.UtcNow.AddDays(45);
            }

            if (!ev.TicketTypes.Any() && ev.Status != EventStatus.Rejected)
            {
                var basePrice = ev.Price > 0 ? ev.Price : 7500.00m;
                context.TicketTypes.AddRange(
                    new TicketType
                    {
                        Id = Guid.NewGuid(),
                        EventId = ev.Id,
                        Name = "General Admission",
                        Price = basePrice,
                        Capacity = 100,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow
                    },
                    new TicketType
                    {
                        Id = Guid.NewGuid(),
                        EventId = ev.Id,
                        Name = "VIP Pass",
                        Price = basePrice * 2 > 0 ? basePrice * 2 : 15000.00m,
                        Capacity = 50,
                        BookedQuantity = 0,
                        CreatedAt = DateTime.UtcNow
                    }
                );
            }
        }

        await context.SaveChangesAsync();
    }
}

