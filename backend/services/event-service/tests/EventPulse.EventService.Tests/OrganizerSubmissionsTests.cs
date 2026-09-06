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
        public Task<string> UploadAsync(Stream imageStream, string contentType, string originalFileName, CancellationToken cancellationToken = default)
            => Task.FromResult("events/fake.webp");

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
}
