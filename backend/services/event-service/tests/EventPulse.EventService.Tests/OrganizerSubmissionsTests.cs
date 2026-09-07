using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Xunit;

namespace EventPulse.EventService.Tests;

public class OrganizerSubmissionsTests
{
    private class FakeImageStorage : IEventImageStorage
    {
        public Task<string> UploadAsync(Stream imageStream, string contentType, string originalFileName, string folderPrefix = "event-posters", CancellationToken cancellationToken = default)
            => Task.FromResult($"{folderPrefix}/fake.webp");

        public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string? GetPublicUrl(string? blobName)
            => string.IsNullOrEmpty(blobName) ? null : $"http://127.0.0.1:10000/devstoreaccount1/event-posters/{blobName}";
    }

    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task GetOrganizerSubmissionsAsync_ReturnsOnlyAuthenticatedOrganizerEvents()
    {
        using var context = CreateContext();
        var organizerA = Guid.NewGuid();
        var organizerB = Guid.NewGuid();

        context.Events.AddRange(
            new Event { Id = Guid.NewGuid(), Title = "Event A1", OrganizerId = organizerA, Status = EventStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Event { Id = Guid.NewGuid(), Title = "Event A2", OrganizerId = organizerA, Status = EventStatus.Published, CreatedAt = DateTime.UtcNow.AddMinutes(1) },
            new Event { Id = Guid.NewGuid(), Title = "Event B1", OrganizerId = organizerB, Status = EventStatus.Pending, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionsAsync(organizerA);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.StartsWith("Event A", item.Title));
    }

    [Fact]
    public async Task GetOrganizerSubmissionsAsync_ReturnsAllStatuses_PendingApprovedRejectedPublished()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();

        context.Events.AddRange(
            new Event { Id = Guid.NewGuid(), Title = "Pending Event", OrganizerId = organizerId, Status = EventStatus.Pending, CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new Event { Id = Guid.NewGuid(), Title = "Approved Event", OrganizerId = organizerId, Status = EventStatus.Approved, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new Event { Id = Guid.NewGuid(), Title = "Rejected Event", OrganizerId = organizerId, Status = EventStatus.Rejected, CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new Event { Id = Guid.NewGuid(), Title = "Published Event", OrganizerId = organizerId, Status = EventStatus.Published, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionsAsync(organizerId);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.Status == "Pending");
        Assert.Contains(result, r => r.Status == "Approved");
        Assert.Contains(result, r => r.Status == "Rejected");
        Assert.Contains(result, r => r.Status == "Published");
    }

    [Fact]
    public async Task GetOrganizerSubmissionsAsync_SortsNewestFirst()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();

        var older = new Event { Id = Guid.NewGuid(), Title = "Older Event", OrganizerId = organizerId, Status = EventStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var newer = new Event { Id = Guid.NewGuid(), Title = "Newer Event", OrganizerId = organizerId, Status = EventStatus.Pending, CreatedAt = DateTime.UtcNow };

        context.Events.AddRange(older, newer);
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionsAsync(organizerId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Newer Event", result[0].Title);
        Assert.Equal("Older Event", result[1].Title);
    }

    [Fact]
    public async Task GetOrganizerSubmissionsAsync_ResolvesImageUrl_AndHandlesNull()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();

        context.Events.AddRange(
            new Event { Id = Guid.NewGuid(), Title = "With Poster", OrganizerId = organizerId, Status = EventStatus.Pending, ImageBlobName = "events/test.webp" },
            new Event { Id = Guid.NewGuid(), Title = "Without Poster", OrganizerId = organizerId, Status = EventStatus.Pending, ImageBlobName = null }
        );
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionsAsync(organizerId);

        var withPoster = result.First(r => r.Title == "With Poster");
        var withoutPoster = result.First(r => r.Title == "Without Poster");

        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/event-posters/events/test.webp", withPoster.ImageUrl);
        Assert.Null(withoutPoster.ImageUrl);
    }

    [Fact]
    public async Task GetOrganizerSubmissionsAsync_ReturnsEmptyList_WhenNoSubmissions()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var result = await service.GetOrganizerSubmissionsAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMySubmissions_ReturnsUnauthorized_WhenNoValidClaim()
    {
        using var context = CreateContext();
        var controller = new EventsController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() // no user claims
        };

        var result = await controller.GetMySubmissions(CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task GetMySubmissions_ReturnsOk_WithSubmissions_WhenOrganizerClaimPresent()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = Guid.NewGuid(),
            Title = "My Pending Submission",
            OrganizerId = organizerId,
            Status = EventStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var controller = new EventsController(context, service);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.GetMySubmissions(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var submissions = Assert.IsAssignableFrom<IReadOnlyList<OrganizerEventSubmissionDto>>(okResult.Value);
        Assert.Single(submissions);
        Assert.Equal("My Pending Submission", submissions[0].Title);
        Assert.Equal("Pending", submissions[0].Status);
    }

    [Fact]
    public async Task GetOrganizerSubmissionByIdAsync_ReturnsSubmission_WhenOwned()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Rejected Workshop",
            Description = "Workshop description",
            Venue = "Colombo Center",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1500,
            OrganizerId = organizerId,
            Status = EventStatus.Rejected,
            ReviewComment = "Please provide exact room number.",
            ImageBlobName = "events/test.jpg"
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionByIdAsync(eventId, organizerId);

        Assert.NotNull(result);
        Assert.Equal("Rejected Workshop", result.Title);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("Please provide exact room number.", result.ReviewComment);
        Assert.NotNull(result.ImageUrl);
    }

    [Fact]
    public async Task GetOrganizerSubmissionByIdAsync_ReturnsNull_WhenOwnedByDifferentOrganizer()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Secret Event",
            OrganizerId = ownerId,
            Status = EventStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var result = await service.GetOrganizerSubmissionByIdAsync(eventId, otherOrganizerId);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResubmitAsync_Succeeds_UpdatesStatusToPending_PreservesIdAndFeedback()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Old Title",
            Description = "Old Description that needs more details here",
            Venue = "Old Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            Price = 2000,
            OrganizerId = organizerId,
            Status = EventStatus.Rejected,
            ReviewComment = "Incomplete description",
            ImageBlobName = "events/original.jpg"
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var request = new ResubmitEventRequest
        {
            Title = "Updated Corrected Title",
            Description = "A brand new and complete description that exceeds minimum length requirements",
            Venue = "Colombo Exhibition Center, Hall A",
            EventDate = DateTime.UtcNow.AddDays(20),
            Price = 2500,
            Category = "Conference",
            VenueType = "Indoor",
            Image = null // Keep existing image
        };

        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await service.ResubmitAsync(eventId, request, organizerId);

        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);
        Assert.NotNull(result);

        // Verify DTO
        Assert.Equal(eventId, result.Id);
        Assert.Equal("Updated Corrected Title", result.Title);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Incomplete description", result.ReviewComment); // Preserved feedback!

        // Verify DB entity
        var dbEvent = await context.Events.FindAsync(eventId);
        Assert.NotNull(dbEvent);
        Assert.Equal(eventId, dbEvent.Id);
        Assert.Equal("Updated Corrected Title", dbEvent.Title);
        Assert.Equal(EventStatus.Pending, dbEvent.Status);
        Assert.Equal("Incomplete description", dbEvent.ReviewComment);
        Assert.Equal("events/original.jpg", dbEvent.ImageBlobName); // Image preserved
    }

    [Fact]
    public async Task ResubmitAsync_ReplacesPoster_AndDeletesOldBlob()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var storage = new FakeImageStorage();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Event with Old Poster",
            Description = "Description long enough to satisfy domain validation requirements",
            Venue = "Nelum Pokuna",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            OrganizerId = organizerId,
            Status = EventStatus.Rejected,
            ImageBlobName = "events/old_poster.png"
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, storage, null!);

        var formFile = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "image", "new_poster.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var request = new ResubmitEventRequest
        {
            Title = "Event with New Poster",
            Description = "Description long enough to satisfy domain validation requirements",
            Venue = "Nelum Pokuna",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Category = "Conference",
            VenueType = "Indoor",
            Image = formFile
        };

        var (result, error, _, _, _) = await service.ResubmitAsync(eventId, request, organizerId);

        Assert.Null(error);
        Assert.NotNull(result);

        var dbEvent = await context.Events.FindAsync(eventId);
        Assert.NotNull(dbEvent);
        Assert.NotEqual("events/old_poster.png", dbEvent.ImageBlobName);
        Assert.Equal(EventStatus.Pending, dbEvent.Status);
    }

    [Theory]
    [InlineData(EventStatus.Pending)]
    [InlineData(EventStatus.Approved)]
    [InlineData(EventStatus.Published)]
    public async Task ResubmitAsync_Rejects_WhenStatusIsNotRejected(EventStatus nonRejectedStatus)
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Non-rejected Event",
            Description = "Valid event description for unit test purposes",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            OrganizerId = organizerId,
            Status = nonRejectedStatus
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var request = new ResubmitEventRequest
        {
            Title = "Attempted Edit",
            Description = "Valid event description for unit test purposes",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            Price = 500,
            Category = "Conference",
            VenueType = "Indoor"
        };

        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await service.ResubmitAsync(eventId, request, organizerId);

        Assert.Null(result);
        Assert.True(isInvalidState);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.Contains("Only Rejected events can be edited and resubmitted", error);
    }

    [Fact]
    public async Task ResubmitAsync_Rejects_WhenDifferentOrganizerAttemptsResubmit()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Rejected Event",
            Description = "Valid event description for unit test purposes",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            OrganizerId = ownerId,
            Status = EventStatus.Rejected
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var request = new ResubmitEventRequest
        {
            Title = "Attempted Edit",
            Description = "Valid event description for unit test purposes",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            Price = 500,
            Category = "Conference",
            VenueType = "Indoor"
        };

        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await service.ResubmitAsync(eventId, request, otherOrganizerId);

        Assert.Null(result);
        Assert.True(isForbidden);
    }

    [Fact]
    public async Task RejectEventAsync_Persists_ReviewComment_ToDatabase()
    {
        using var context = CreateContext();
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Event to Reject",
            OrganizerId = Guid.NewGuid(),
            Status = EventStatus.Pending
        });
        await context.SaveChangesAsync();

        var reviewService = new EventReviewService(context, new FakeImageStorage());
        var (result, error, isNotFound) = await reviewService.RejectEventAsync(
            eventId,
            adminId,
            "The event date is unrealistic and poster is missing text."
        );

        Assert.Null(error);
        Assert.False(isNotFound);
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("The event date is unrealistic and poster is missing text.", result.ReviewComment);

        var dbEvent = await context.Events.FindAsync(eventId);
        Assert.NotNull(dbEvent);
        Assert.Equal(EventStatus.Rejected, dbEvent.Status);
        Assert.Equal("The event date is unrealistic and poster is missing text.", dbEvent.ReviewComment);
    }

    [Fact]
    public async Task GetMySubmissionById_ReturnsOk_WhenOwned()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "My Submission",
            OrganizerId = organizerId,
            Status = EventStatus.Rejected,
            ReviewComment = "Fix venue details."
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var controller = new EventsController(context, service);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await controller.GetMySubmissionById(eventId.ToString(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var submission = Assert.IsType<OrganizerEventSubmissionDto>(okResult.Value);
        Assert.Equal(eventId, submission.Id);
        Assert.Equal("Fix venue details.", submission.ReviewComment);
    }

    [Fact]
    public async Task GetMySubmissionById_ReturnsNotFound_WhenEventBelongsToOtherOrganizer()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var requestingOrganizer = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Another Organizer's Submission",
            OrganizerId = ownerId,
            Status = EventStatus.Rejected
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var controller = new EventsController(context, service);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, requestingOrganizer.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await controller.GetMySubmissionById(eventId.ToString(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ResubmitEvent_Controller_ReturnsOk_AndTransitionsToPending()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Old Rejected Event",
            Description = "Initial event description text that needs fixing",
            Venue = "Old Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            Price = 1000,
            OrganizerId = organizerId,
            Status = EventStatus.Rejected,
            ReviewComment = "Needs new date"
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var controller = new EventsController(context, service);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var request = new ResubmitEventRequest
        {
            Title = "Corrected Event Title",
            Description = "Updated event description with sufficient characters",
            Venue = "New Venue, Colombo",
            EventDate = DateTime.UtcNow.AddDays(15),
            Price = 1200,
            Category = "Conference",
            VenueType = "Indoor"
        };

        var result = await controller.ResubmitEvent(eventId.ToString(), request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var submission = Assert.IsType<OrganizerEventSubmissionDto>(okResult.Value);
        Assert.Equal(eventId, submission.Id);
        Assert.Equal("Pending", submission.Status);
        Assert.Equal("Needs new date", submission.ReviewComment);
    }

    [Fact]
    public async Task ResubmitEvent_Controller_ReturnsConflict_WhenEventIsNotRejected()
    {
        using var context = CreateContext();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Title = "Already Approved Event",
            Description = "Event description",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            OrganizerId = organizerId,
            Status = EventStatus.Approved
        });
        await context.SaveChangesAsync();

        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var controller = new EventsController(context, service);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var request = new ResubmitEventRequest
        {
            Title = "Updated Title",
            Description = "Updated Description long enough",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Category = "Conference",
            VenueType = "Indoor"
        };

        var result = await controller.ResubmitEvent(eventId.ToString(), request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task EndToEnd_Workflow_Steps_A_Through_J()
    {
        using var context = CreateContext();
        var fakeStorage = new FakeImageStorage();
        var submissionService = new EventSubmissionService(context, fakeStorage, null!);
        var reviewService = new EventReviewService(context, fakeStorage, null!);

        var organizerId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var originalBlob = "events/initial_poster.webp";

        // Initial state: Organizer has submitted an event, currently Pending
        var eventId = Guid.NewGuid();
        var initialEvent = new Event
        {
            Id = eventId,
            Title = "Tech Conference 2026",
            Description = "A conference on cutting-edge software and cloud technologies.",
            Venue = "BMICH Main Hall",
            EventDate = DateTime.UtcNow.AddDays(30),
            Price = 5000,
            Status = EventStatus.Pending,
            OrganizerId = organizerId,
            ImageBlobName = originalBlob,
            CreatedAt = DateTime.UtcNow
        };
        context.Events.Add(initialEvent);
        await context.SaveChangesAsync();

        // 0. Verify rejecting with empty notes is rejected (Validation failure)
        var (emptyResult, emptyError, _) = await reviewService.RejectEventAsync(eventId, reviewerId, "   ");
        Assert.Null(emptyResult);
        Assert.Equal("Rejection feedback is required.", emptyError);

        // A. Reject event WITH review notes
        var rejectionReason = "Please provide specific session timings and update the venue hall number.";
        var (rejectResult, rejectError, isNotFound) = await reviewService.RejectEventAsync(eventId, reviewerId, rejectionReason);

        Assert.False(isNotFound);
        Assert.Null(rejectError);
        Assert.NotNull(rejectResult);
        Assert.Equal("Rejected", rejectResult.Status);
        Assert.Equal(rejectionReason, rejectResult.ReviewComment);

        // B. Confirm notes are persisted in database
        var dbAfterReject = await context.Events.FindAsync(eventId);
        Assert.NotNull(dbAfterReject);
        Assert.Equal(EventStatus.Rejected, dbAfterReject.Status);
        Assert.Equal(rejectionReason, dbAfterReject.ReviewComment);
        Assert.Equal(reviewerId, dbAfterReject.ReviewedBy);
        Assert.NotNull(dbAfterReject.ReviewedAt);

        // C. Confirm GET organizer event details returns notes in JSON response
        var organizerDto = await submissionService.GetOrganizerSubmissionByIdAsync(eventId, organizerId);
        Assert.NotNull(organizerDto);
        Assert.Equal(eventId, organizerDto.Id);
        Assert.Equal("Rejected", organizerDto.Status);
        Assert.Equal(rejectionReason, organizerDto.ReviewComment);
        Assert.NotNull(organizerDto.ImageUrl);
        Assert.Contains(originalBlob, organizerDto.ImageUrl);

        // E & F. Organizer edits fields (Title, Venue, Date, etc.) WITHOUT uploading new poster
        var resubmitRequestNoImage = new ResubmitEventRequest
        {
            Title = "Tech Conference 2026 - Updated Hall",
            Description = "Updated description with full detailed morning and afternoon session timings.",
            Venue = "BMICH Hall 3, Colombo",
            EventDate = DateTime.UtcNow.AddDays(35),
            Price = 4500,
            Category = "Conference",
            VenueType = "Indoor",
            Image = null // No replacement poster
        };

        var (resubmitResult, resubmitError, resNotFound, resForbidden, resInvalidState) =
            await submissionService.ResubmitAsync(eventId, resubmitRequestNoImage, organizerId);

        Assert.Null(resubmitError);
        Assert.False(resNotFound);
        Assert.False(resForbidden);
        Assert.False(resInvalidState);
        Assert.NotNull(resubmitResult);

        // G. Confirm status transitions Rejected -> Pending
        Assert.Equal("Pending", resubmitResult.Status);

        // H. Confirm previous ReviewComment is still retained / visible
        Assert.Equal(rejectionReason, resubmitResult.ReviewComment);

        // I. Confirm poster is retained (not lost or overwritten with null/empty)
        Assert.NotNull(resubmitResult.ImageUrl);
        Assert.Contains(originalBlob, resubmitResult.ImageUrl);

        var dbAfterResubmit = await context.Events.FindAsync(eventId);
        Assert.NotNull(dbAfterResubmit);
        Assert.Equal(EventStatus.Pending, dbAfterResubmit.Status);
        Assert.Equal(originalBlob, dbAfterResubmit.ImageBlobName);
        Assert.Equal(rejectionReason, dbAfterResubmit.ReviewComment);
        Assert.Equal("Tech Conference 2026 - Updated Hall", dbAfterResubmit.Title);
        Assert.Equal(organizerId, dbAfterResubmit.OrganizerId); // Ownership preserved
        Assert.Equal(eventId, dbAfterResubmit.Id); // ID preserved

        // J. Organizer resubmits WITH a replacement poster, confirming image updates cleanly
        // First, admin rejects the resubmission with new feedback
        var secondRejection = "Poster needs to have the new hall number printed on it.";
        await reviewService.RejectEventAsync(eventId, reviewerId, secondRejection);

        var dbSecondReject = await context.Events.FindAsync(eventId);
        Assert.Equal(EventStatus.Rejected, dbSecondReject!.Status);
        Assert.Equal(secondRejection, dbSecondReject.ReviewComment);

        // Resubmit with new replacement image file
        var memoryStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 });
        var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "image", "new_poster.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var resubmitWithImage = new ResubmitEventRequest
        {
            Title = "Tech Conference 2026 - Final",
            Description = "Updated description with full detailed morning and afternoon session timings.",
            Venue = "BMICH Hall 3, Colombo",
            EventDate = DateTime.UtcNow.AddDays(40),
            Price = 4500,
            Category = "Conference",
            VenueType = "Indoor",
            Image = formFile
        };

        var (resubmitImgResult, imgError, _, _, _) =
            await submissionService.ResubmitAsync(eventId, resubmitWithImage, organizerId);

        Assert.Null(imgError);
        Assert.NotNull(resubmitImgResult);
        Assert.Equal("Pending", resubmitImgResult.Status);
        Assert.Equal(secondRejection, resubmitImgResult.ReviewComment);
        Assert.Equal("event-posters/fake.webp", dbSecondReject.ImageBlobName);
        Assert.NotNull(resubmitImgResult.ImageUrl);
    }

    [Fact]
    public async Task CreateAsync_WhenPosterMissing_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var coverStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var coverFile = new FormFile(coverStream, 0, 3, "coverImage", "cover.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var request = new CreateEventRequest
        {
            Title = "Valid Title",
            Description = "Long enough event description for validation",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Category = "Conference",
            VenueType = "Indoor",
            Image = null!, // Missing poster
            CoverImage = coverFile
        };

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());
        Assert.Null(result);
        Assert.Equal("Event poster image is required.", error);
    }

    [Fact]
    public async Task CreateAsync_WhenCoverMissing_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var posterStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var posterFile = new FormFile(posterStream, 0, 3, "image", "poster.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var request = new CreateEventRequest
        {
            Title = "Valid Title",
            Description = "Long enough event description for validation",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Category = "Conference",
            VenueType = "Indoor",
            Image = posterFile,
            CoverImage = null! // Missing cover
        };

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());
        Assert.Null(result);
        Assert.Equal("Event cover image is required.", error);
    }

    [Fact]
    public async Task CreateAsync_WhenBothPosterAndCoverProvided_PersistsBothBlobsAndPopulatesBothUrls()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var posterStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var posterFile = new FormFile(posterStream, 0, 3, "image", "poster.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var coverStream = new MemoryStream(new byte[] { 4, 5, 6 });
        var coverFile = new FormFile(coverStream, 0, 3, "coverImage", "cover.webp")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/webp"
        };

        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Title = "Valid Dual Image Event",
            Description = "Long enough event description for validation",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Category = "Conference",
            VenueType = "Indoor",
            Image = posterFile,
            CoverImage = coverFile
        };

        var (result, error) = await service.CreateAsync(request, organizerId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageUrl);
        Assert.NotNull(result.CoverUrl);
        Assert.Contains("event-posters/fake.webp", result.ImageUrl);
        Assert.Contains("event-covers/fake.webp", result.CoverUrl);

        var dbEvent = await context.Events.FindAsync(result.Id);
        Assert.NotNull(dbEvent);
        Assert.Equal("event-posters/fake.webp", dbEvent.ImageBlobName);
        Assert.Equal("event-covers/fake.webp", dbEvent.CoverBlobName);
    }

    [Fact]
    public async Task ResubmitAsync_WhenOnlyCoverReplaced_RetainsPosterAndReplacesCover()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var existingEvent = new Event
        {
            Id = eventId,
            Title = "Event with Poster",
            Description = "Description long enough",
            Venue = "Colombo",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Status = EventStatus.Rejected,
            OrganizerId = organizerId,
            ImageBlobName = "event-posters/original-poster.webp",
            CoverBlobName = "event-covers/original-cover.webp"
        };
        context.Events.Add(existingEvent);
        await context.SaveChangesAsync();

        var coverStream = new MemoryStream(new byte[] { 7, 8, 9 });
        var newCoverFile = new FormFile(coverStream, 0, 3, "coverImage", "new-cover.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var request = new ResubmitEventRequest
        {
            Title = "Updated Event with New Cover",
            Description = "Description long enough",
            Venue = "Colombo",
            EventDate = DateTime.UtcNow.AddDays(12),
            Price = 1200,
            Category = "Conference",
            VenueType = "Indoor",
            Image = null, // No replacement poster
            CoverImage = newCoverFile // Replacement cover provided
        };

        var (result, error, _, _, _) = await service.ResubmitAsync(eventId, request, organizerId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("event-posters/original-poster.webp", existingEvent.ImageBlobName); // Retained
        Assert.Equal("event-covers/fake.webp", existingEvent.CoverBlobName); // Replaced
    }

    [Theory]
    [InlineData(1499.98)]
    [InlineData(15000.83)]
    [InlineData(10.5)]
    [InlineData(0.01)]
    public async Task CreateAsync_RejectsFractionalPrice(decimal fractionalPrice)
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var posterStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var posterFile = new FormFile(posterStream, 0, 3, "image", "poster.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var coverStream = new MemoryStream(new byte[] { 4, 5, 6 });
        var coverFile = new FormFile(coverStream, 0, 3, "coverImage", "cover.webp")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/webp"
        };

        var request = new CreateEventRequest
        {
            Title = "Valid Event",
            Description = "A valid event description for testing ticket price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = fractionalPrice,
            Category = "Conference",
            VenueType = "Indoor",
            Image = posterFile,
            CoverImage = coverFile
        };

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Ticket price must be entered in whole LKR.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(15000)]
    public async Task CreateAsync_AcceptsWholeRupeePrice(decimal wholePrice)
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);

        var posterStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var posterFile = new FormFile(posterStream, 0, 3, "image", "poster.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var coverStream = new MemoryStream(new byte[] { 4, 5, 6 });
        var coverFile = new FormFile(coverStream, 0, 3, "coverImage", "cover.webp")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/webp"
        };

        var request = new CreateEventRequest
        {
            Title = "Valid Event",
            Description = "A valid event description for testing ticket price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = wholePrice,
            Category = "Conference",
            VenueType = "Indoor",
            Image = posterFile,
            CoverImage = coverFile
        };

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal(wholePrice, result.Price);
    }

    [Theory]
    [InlineData(1499.98)]
    [InlineData(15000.83)]
    [InlineData(0.50)]
    public async Task ResubmitAsync_RejectsFractionalPrice(decimal fractionalPrice)
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var existingEvent = new Event
        {
            Id = eventId,
            Title = "Rejected Event",
            Description = "A valid event description for testing ticket price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Status = EventStatus.Rejected,
            OrganizerId = organizerId,
            ImageBlobName = "event-posters/original-poster.webp",
            CoverBlobName = "event-covers/original-cover.webp"
        };
        context.Events.Add(existingEvent);
        await context.SaveChangesAsync();

        var request = new ResubmitEventRequest
        {
            Title = "Updated Event",
            Description = "A valid updated description for testing price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(12),
            Price = fractionalPrice,
            Category = "Conference",
            VenueType = "Indoor",
        };

        var (result, error, _, _, _) = await service.ResubmitAsync(eventId, request, organizerId);

        Assert.Null(result);
        Assert.Equal("Ticket price must be entered in whole LKR.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15000)]
    public async Task ResubmitAsync_AcceptsWholeRupeePrice(decimal wholePrice)
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, new FakeImageStorage(), null!);
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var existingEvent = new Event
        {
            Id = eventId,
            Title = "Rejected Event",
            Description = "A valid event description for testing ticket price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 1000,
            Status = EventStatus.Rejected,
            OrganizerId = organizerId,
            ImageBlobName = "event-posters/original-poster.webp",
            CoverBlobName = "event-covers/original-cover.webp"
        };
        context.Events.Add(existingEvent);
        await context.SaveChangesAsync();

        var request = new ResubmitEventRequest
        {
            Title = "Updated Event",
            Description = "A valid updated description for testing price precision.",
            Venue = "BMICH",
            EventDate = DateTime.UtcNow.AddDays(12),
            Price = wholePrice,
            Category = "Conference",
            VenueType = "Indoor",
        };

        var (result, error, _, _, _) = await service.ResubmitAsync(eventId, request, organizerId);

        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal(wholePrice, result.Price);
    }
}
