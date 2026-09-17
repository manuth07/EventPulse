using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Data;

public static class EventDbSeeder
{
    public static async Task SeedAsync(EventDbContext context)
    {
        if (await context.Events.AnyAsync())
        {
            // Backfill existing seed events that have null or legacy Category/VenueType
            var legacyEvents = await context.Events.Where(e => e.Category == null || e.Category == "Musical Concert" || e.Category == "Theatre / Performance").ToListAsync();
            if (legacyEvents.Any())
            {
                foreach (var ev in legacyEvents)
                {
                    if (ev.Category == "Musical Concert")
                    {
                        ev.Category = "Music";
                    }
                    else if (ev.Category == "Theatre / Performance")
                    {
                        ev.Category = "Arts & Theatre";
                    }
                    else if (ev.Id == Guid.Parse("a1111111-1111-1111-1111-111111111111"))
                    {
                        ev.Category = "Conference";
                        ev.VenueType = "Indoor";
                    }
                    else if (ev.Id == Guid.Parse("b2222222-2222-2222-2222-222222222222"))
                    {
                        ev.Category = "Music";
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
        var organizer3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var organizer4 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var adminId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var events = new List<Event>
        {
            new Event
            {
                Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                Title = "Tech Conference 2026",
                Description = "Annual flagship technology and software engineering conference in Sri Lanka featuring global keynotes, architecture tracks, and developer panels.",
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
                Description = "Outdoor live music performance featuring top national and international artists, live bands, food stalls, and sunset vibes.",
                Venue = "Galle Face Green, Colombo",
                EventDate = DateTime.UtcNow.AddDays(45),
                Price = 3500.00m,
                Category = "Music",
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
                Description = "Networking and pitch event for early stage Sri Lankan tech startups, founders, venture capitalists, and angel investors.",
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
                Description = "Hands-on workshop covering LLMs, Agentic AI systems, fine-tuning, and modern machine learning production deployment.",
                Venue = "SLIIT Auditorium, Malabe",
                EventDate = DateTime.UtcNow.AddDays(60),
                Price = 2500.00m,
                Category = "Workshop",
                VenueType = "Indoor",
                Status = EventStatus.Published,
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
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
                Title = "Sri Lanka National Hackathon 2026",
                Description = "36-hour non-stop hackathon challenging university and industry builders to create groundbreaking FinTech, HealthTech, and AI solutions.",
                Venue = "Trace Expert City, Bay 7, Colombo 10",
                EventDate = DateTime.UtcNow.AddDays(20),
                Price = 0.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                ReviewedAt = DateTime.UtcNow.AddDays(-7),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("a7777777-7777-7777-7777-777777777777"),
                Title = "Cyber Security Summit Colombo",
                Description = "Premier cybersecurity summit exploring zero trust architectures, cloud security posture management, and cyber resilience in enterprise.",
                Venue = "Hilton Colombo, Grand Ballroom",
                EventDate = DateTime.UtcNow.AddDays(35),
                Price = 7500.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-9),
                ReviewedAt = DateTime.UtcNow.AddDays(-3),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("b8888888-8888-8888-8888-888888888888"),
                Title = "Kandy Cultural Rhythms & Drum Fest",
                Description = "An immersive evening of traditional Kandyan drumming, contemporary fusion percussion, and vibrant cultural dance performances.",
                Venue = "Bogambara Cultural Grounds, Kandy",
                EventDate = DateTime.UtcNow.AddDays(25),
                Price = 2000.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer3,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                ReviewedAt = DateTime.UtcNow.AddDays(-8),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("c9999999-9999-9999-9999-999999999999"),
                Title = "Galle Literary & Heritage Showcase",
                Description = "Celebration of literature, storytelling, architectural heritage, and historical discussions within the iconic ramparts of Galle Fort.",
                Venue = "Galle Fort Ramparts, Galle",
                EventDate = DateTime.UtcNow.AddDays(50),
                Price = 3000.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer3,
                CreatedAt = DateTime.UtcNow.AddDays(-18),
                ReviewedAt = DateTime.UtcNow.AddDays(-10),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("d1010101-1010-1010-1010-101010101010"),
                Title = "Esports Championship Sri Lanka 2026",
                Description = "National competitive gaming arena with thrilling tournaments in Valorant, Dota 2, and EA FC 26 with major cash prize pools.",
                Venue = "SLECC (Sri Lanka Exhibition Centre), Colombo",
                EventDate = DateTime.UtcNow.AddDays(40),
                Price = 1500.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer4,
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                ReviewedAt = DateTime.UtcNow.AddDays(-6),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("e2020202-2020-2020-2020-202020202020"),
                Title = "Ceylon Food & Street Feast 2026",
                Description = "A weekend culinary adventure featuring authentic Sri Lankan street food, artisan desserts, live cooking demos, and acoustic music.",
                Venue = "Viharamahadevi Park, Colombo 07",
                EventDate = DateTime.UtcNow.AddDays(18),
                Price = 0.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ReviewedAt = DateTime.UtcNow.AddDays(-2),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("f3030303-3030-3030-3030-303030303030"),
                Title = "Cloud Native & DevOps Summit",
                Description = "Deep-dive technical sessions on Kubernetes, GitOps, distributed tracing, and infrastructure as code by industry experts.",
                Venue = "Cinnamon Lakeside Ballroom, Colombo",
                EventDate = DateTime.UtcNow.AddDays(55),
                Price = 4500.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-11),
                ReviewedAt = DateTime.UtcNow.AddDays(-4),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("a4040404-4040-4040-4040-404040404040"),
                Title = "UX/UI Design & Product Sprint",
                Description = "Interactive workshop on design systems, Figma component architectures, accessibility standards, and usability testing.",
                Venue = "Dialog Axiata Auditorium, Colombo 02",
                EventDate = DateTime.UtcNow.AddDays(28),
                Price = 2200.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                ReviewedAt = DateTime.UtcNow.AddDays(-3),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("b5050505-5050-5050-5050-505050505050"),
                Title = "Photography Masterclass & Photowalk",
                Description = "Learn professional framing, lighting techniques, and color grading followed by an afternoon street photowalk through Colombo.",
                Venue = "National Museum Grounds, Colombo 07",
                EventDate = DateTime.UtcNow.AddDays(22),
                Price = 3200.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer3,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                ReviewedAt = DateTime.UtcNow.AddDays(-2),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("c6060606-6060-6060-6060-606060606060"),
                Title = "Jazz & Blues Under the Stars",
                Description = "An intimate open-air evening featuring soulful live jazz, blues ensembles, fine dining, and cocktails beside the lake.",
                Venue = "Water's Edge Garden Pavilion, Battaramulla",
                EventDate = DateTime.UtcNow.AddDays(38),
                Price = 6000.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer2,
                CreatedAt = DateTime.UtcNow.AddDays(-16),
                ReviewedAt = DateTime.UtcNow.AddDays(-9),
                ReviewedBy = adminId
            },
            new Event
            {
                Id = Guid.Parse("d7070707-7070-7070-7070-707070707070"),
                Title = "Inter-University Robotics Showcase",
                Description = "Showcase of autonomous robotics, drone agility trials, and IoT innovations created by undergraduate engineering teams.",
                Venue = "University of Moratuwa Campus Grounds",
                EventDate = DateTime.UtcNow.AddDays(42),
                Price = 0.00m,
                Status = EventStatus.Pending,
                OrganizerId = organizer1,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ReviewedAt = null,
                ReviewedBy = null
            },
            new Event
            {
                Id = Guid.Parse("e8080808-8080-8080-8080-808080808080"),
                Title = "Wellness & Beach Yoga Festival",
                Description = "Rejuvenating sunrise yoga sessions, guided mindfulness meditation, breathwork, and clean nutrition seminars by the ocean.",
                Venue = "Mount Lavinia Beach Terrace",
                EventDate = DateTime.UtcNow.AddDays(12),
                Price = 1800.00m,
                Status = EventStatus.Published,
                OrganizerId = organizer4,
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ReviewedAt = DateTime.UtcNow.AddDays(-1),
                ReviewedBy = adminId
                ReviewedBy = adminId,
                ReviewComment = "The venue address is incomplete. Please provide the full physical venue address and detailed event schedule."
            }
        };

        var existingIds = await context.Events.Select(e => e.Id).ToHashSetAsync();
        var newEvents = events.Where(e => !existingIds.Contains(e.Id)).ToList();

        if (newEvents.Count > 0)
        {
            await context.Events.AddRangeAsync(newEvents);
            await context.SaveChangesAsync();
        }
    }
}
