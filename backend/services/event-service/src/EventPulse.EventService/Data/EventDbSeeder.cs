using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Data;

public static class EventDbSeeder
{
    public static async Task SeedAsync(EventDbContext context)
    {
        var organizer1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var organizer2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var organizer3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var organizer4 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var adminId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var now = DateTime.UtcNow;

        var eventDetails = new (string Id, string Title, string Description, string Venue, int Days, decimal Price, string Category, string VenueType, Guid OrganizerId)[]
        {
            ("a1111111-1111-1111-1111-111111111111", "Tech Conference 2026", "Annual flagship technology and software engineering conference in Sri Lanka featuring global keynotes, architecture tracks, and developer panels.", "BMICH, Colombo", 45, 7500m, "Conference", "Indoor", organizer1),
            ("b2222222-2222-2222-2222-222222222222", "Colombo Music Festival", "Outdoor live music performance featuring top national and international artists, live bands, food stalls, and sunset vibes.", "Galle Face Green, Colombo", 60, 5000m, "Music", "Outdoor", organizer2),
            ("c3333333-3333-3333-3333-333333333333", "Startup Meetup 2026", "Networking and pitch event for early stage Sri Lankan tech startups, founders, venture capitalists, and angel investors.", "Trace Expert City, Colombo 10", 30, 0m, "Conference", "Indoor", organizer1),
            ("d4444444-4444-4444-4444-444444444444", "AI Workshop Sri Lanka", "Hands-on workshop covering LLMs, Agentic AI systems, fine-tuning, and modern machine learning production deployment.", "SLIIT Auditorium, Malabe", 75, 2500m, "Workshop", "Indoor", organizer2),
            ("e5555555-5555-5555-5555-555555555555", "Rejected Test Event", "Sample event that failed verification standards and was rejected by admin.", "Virtual / Online", 20, 1000m, "Other", "Indoor", organizer1),
            ("f6666666-6666-6666-6666-666666666666", "Cyber Security Summit Colombo", "Sri Lanka's premier cyber security summit gathering global security leaders, ethical hackers, and CISOs.", "Cinnamon Grand, Colombo", 50, 7500m, "Conference", "Indoor", organizer1),
            ("77777777-7777-7777-7777-777777777777", "Wellness & Beach Yoga Festival", "A weekend of mindfulness, beach yoga sessions, meditation, and holistic health workshops.", "Mount Lavinia Beach Hotel", 70, 4000m, "Festival", "Outdoor", organizer2),
            ("01010101-0101-0101-0101-010101010101", "Sri Lanka National Hackathon 2026", "36-hour non-stop hackathon challenging university and industry builders to create groundbreaking FinTech, HealthTech, and AI solutions.", "Trace Expert City, Bay 7, Colombo 10", 20, 0m, "Technology", "Indoor", organizer1),
            ("a7777777-7777-7777-7777-777777777777", "Cyber Security Summit Colombo", "Premier cybersecurity summit exploring zero trust architectures, cloud security posture management, and cyber resilience in enterprise.", "Hilton Colombo, Grand Ballroom", 35, 7500m, "Conference", "Indoor", organizer1),
            ("b8888888-8888-8888-8888-888888888888", "Kandy Cultural Rhythms & Drum Fest", "An immersive evening of traditional Kandyan drumming, contemporary fusion percussion, and vibrant cultural dance performances.", "Bogambara Cultural Grounds, Kandy", 25, 2000m, "Festival", "Outdoor", organizer3),
            ("c9999999-9999-9999-9999-999999999999", "Galle Literary & Heritage Showcase", "Celebration of literature, storytelling, architectural heritage, and historical discussions within the iconic ramparts of Galle Fort.", "Galle Fort Ramparts, Galle", 50, 3000m, "Literature", "Outdoor", organizer3),
            ("d1010101-1010-1010-1010-101010101010", "Esports Championship Sri Lanka 2026", "National competitive gaming arena with thrilling tournaments in Valorant, Dota 2, and EA FC 26 with major cash prize pools.", "SLECC (Sri Lanka Exhibition Centre), Colombo", 40, 1500m, "Esports", "Indoor", organizer4),
            ("e2020202-2020-2020-2020-202020202020", "Ceylon Food & Street Feast 2026", "A weekend culinary adventure featuring authentic Sri Lankan street food, artisan desserts, live cooking demos, and acoustic music.", "Viharamahadevi Park, Colombo 07", 18, 0m, "Food", "Outdoor", organizer2),
            ("f3030303-3030-3030-3030-303030303030", "Cloud Native & DevOps Summit", "Deep-dive technical sessions on Kubernetes, GitOps, distributed tracing, and infrastructure as code by industry experts.", "Cinnamon Lakeside Ballroom, Colombo", 55, 4500m, "Technology", "Indoor", organizer1),
            ("a4040404-4040-4040-4040-404040404040", "UX/UI Design & Product Sprint", "Interactive workshop on design systems, Figma component architectures, accessibility standards, and usability testing.", "Dialog Axiata Auditorium, Colombo 02", 28, 2200m, "Workshop", "Indoor", organizer2),
            ("b5050505-5050-5050-5050-505050505050", "Photography Masterclass & Photowalk", "Learn professional framing, lighting techniques, and color grading followed by an afternoon street photowalk through Colombo.", "National Museum Grounds, Colombo 07", 22, 3200m, "Workshop", "Outdoor", organizer3),
            ("c6060606-6060-6060-6060-606060606060", "Jazz & Blues Under the Stars", "An intimate open-air evening featuring soulful live jazz, blues ensembles, fine dining, and cocktails beside the lake.", "Water's Edge Garden Pavilion, Battaramulla", 38, 6000m, "Music", "Outdoor", organizer2),
            ("d7070707-7070-7070-7070-707070707070", "Inter-University Robotics Showcase", "Showcase of autonomous robotics, drone agility trials, and IoT innovations created by undergraduate engineering teams.", "University of Moratuwa Campus Grounds", 42, 0m, "Technology", "Outdoor", organizer1),
            ("e8080808-8080-8080-8080-808080808080", "Wellness & Beach Yoga Festival", "Rejuvenating sunrise yoga sessions, guided mindfulness meditation, breathwork, and clean nutrition seminars by the ocean.", "Mount Lavinia Beach Terrace", 12, 1800m, "Festival", "Outdoor", organizer4)
        };

        var seedEvents = eventDetails.Select(details => new Event
        {
            Id = Guid.Parse(details.Id),
            Title = details.Title,
            Description = details.Description,
            Venue = details.Venue,
            EventDate = now.AddDays(details.Days),
            Price = details.Price,
            Category = details.Category,
            VenueType = details.VenueType,
            Status = EventStatus.Published,
            OrganizerId = details.OrganizerId,
            CreatedAt = now.AddDays(-5),
            ReviewedAt = now,
            ReviewedBy = adminId
        }).ToList();

        foreach (var seedEvent in seedEvents)
        {
            seedEvent.TicketTypes = new List<TicketType>
            {
                CreateTicket(seedEvent, "General Admission", now),
                CreateTicket(seedEvent, "VIP Pass", now)
            };
        }

        foreach (var seedEvent in seedEvents)
        {
            var existingEvent = await context.Events
                .Include(eventItem => eventItem.TicketTypes)
                .FirstOrDefaultAsync(eventItem => eventItem.Id == seedEvent.Id);

            if (existingEvent is null)
            {
                await context.Events.AddAsync(seedEvent);
                continue;
            }

            existingEvent.Status = EventStatus.Published;
            if (existingEvent.EventDate <= now)
            {
                existingEvent.EventDate = seedEvent.EventDate;
            }

            if (string.IsNullOrEmpty(existingEvent.Category))
            {
                existingEvent.Category = seedEvent.Category;
            }

            if (string.IsNullOrEmpty(existingEvent.VenueType))
            {
                existingEvent.VenueType = seedEvent.VenueType;
            }

            if (!existingEvent.TicketTypes.Any(ticket => ticket.Name == "General Admission"))
            {
                await context.TicketTypes.AddAsync(CreateTicket(existingEvent, "General Admission", now));
            }

            if (!existingEvent.TicketTypes.Any(ticket => ticket.Name == "VIP Pass"))
            {
                await context.TicketTypes.AddAsync(CreateTicket(existingEvent, "VIP Pass", now));
            }
        }

        var allEvents = await context.Events
            .Include(eventItem => eventItem.TicketTypes)
            .ToListAsync();

        foreach (var eventItem in allEvents)
        {
            if (eventItem.EventDate <= now)
            {
                eventItem.EventDate = now.AddDays(45);
            }

            if (eventItem.Status != EventStatus.Rejected)
            {
                if (!eventItem.TicketTypes.Any(ticket => ticket.Name == "General Admission"))
                {
                    await context.TicketTypes.AddAsync(CreateTicket(eventItem, "General Admission", now));
                }

                if (!eventItem.TicketTypes.Any(ticket => ticket.Name == "VIP Pass"))
                {
                    await context.TicketTypes.AddAsync(CreateTicket(eventItem, "VIP Pass", now));
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static TicketType CreateTicket(Event eventItem, string name, DateTime createdAt)
    {
        var isVip = name == "VIP Pass";
        var basePrice = eventItem.Price > 0 ? eventItem.Price : 0m;

        return new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventItem.Id,
            Name = name,
            Price = isVip ? basePrice * 2 : basePrice,
            Capacity = isVip ? 50 : 100,
            BookedQuantity = 0,
            CreatedAt = createdAt
        };
    }
}

