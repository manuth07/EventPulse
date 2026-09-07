using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventSubmissionServiceCreateAsyncTests
{
    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    // NOTE: adjust this mock's method signature to match your CURRENT IEventImageStorage
    // interface — this assumes the 5-arg UploadAsync(stream, contentType, fileName, folder, ct)
    // implied by EventSubmissionService.CreateAsync's call site.
    private static Mock<IEventImageStorage> MockStorage()
    {
        var mock = new Mock<IEventImageStorage>();
        mock.Setup(m => m.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string _, string fileName, string folder, CancellationToken _)
                => $"{folder}/{Guid.NewGuid()}-{fileName}");
        mock.Setup(m => m.GetPublicUrl(It.IsAny<string?>()))
            .Returns((string? blob) => blob is null ? null : $"http://127.0.0.1:10000/devstoreaccount1/{blob}");
        return mock;
    }

    private static IFormFile MakeFormFile(string contentType, long sizeBytes, string fileName = "test.jpg")
    {
        var bytes = new byte[Math.Max(sizeBytes, 1)];
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(sizeBytes);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    private static CreateEventRequest ValidRequest() => new()
    {
        Title = "Valid Event",
        Description = "A valid description",
        Venue = "Some Venue",
        EventDate = DateTime.UtcNow.AddDays(30),
        Price = 1000m,
        Image = MakeFormFile("image/jpeg", 1024, "poster.jpg"),
        CoverImage = MakeFormFile("image/jpeg", 2048, "cover.jpg"),
    };

    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsPendingEventAndReturnsBothUrls()
    {
        using var context = CreateContext();
        var storage = MockStorage();
        var service = new EventSubmissionService(context, storage.Object, null);
        var organizerId = Guid.NewGuid();

        var (result, error) = await service.CreateAsync(ValidRequest(), organizerId);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("Pending", result!.Status);
        Assert.Equal(organizerId, result.OrganizerId);
        Assert.NotNull(result.ImageUrl);
        Assert.NotNull(result.CoverUrl);

        var saved = await context.Events.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal(Models.EventStatus.Pending, saved!.Status);
        Assert.NotNull(saved.ImageBlobName);
        Assert.NotNull(saved.CoverBlobName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_MissingTitle_ReturnsError(string title)
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Title = title;

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Title is required.", error);
    }

    [Fact]
    public async Task CreateAsync_PastEventDate_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.EventDate = DateTime.UtcNow.AddDays(-1);

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("EventDate must be in the future.", error);
    }

    [Fact]
    public async Task CreateAsync_NegativePrice_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Price = -100m;

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Price must be 0 or greater.", error);
    }

    [Fact]
    public async Task CreateAsync_FractionalPrice_ReturnsWholeLkrError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Price = 999.50m;

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Ticket price must be entered in whole LKR.", error);
    }

    [Fact]
    public async Task CreateAsync_MissingPosterImage_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Image = null!;

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Event poster image is required.", error);
    }

    [Fact]
    public async Task CreateAsync_UnsupportedPosterContentType_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Image = MakeFormFile("text/plain", 100, "fake.txt");

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Contains("Unsupported poster image type", error);
    }

    [Fact]
    public async Task CreateAsync_OversizedPoster_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.Image = MakeFormFile("image/jpeg", 6 * 1024 * 1024); // 6 MB > 5 MB limit

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Contains("exceeds the 5 MB maximum", error);
    }

    [Fact]
    public async Task CreateAsync_MissingCoverImage_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.CoverImage = null!;

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Event cover image is required.", error);
    }

    [Fact]
    public async Task CreateAsync_OversizedCover_ReturnsError()
    {
        using var context = CreateContext();
        var service = new EventSubmissionService(context, MockStorage().Object, null);
        var request = ValidRequest();
        request.CoverImage = MakeFormFile("image/jpeg", 6 * 1024 * 1024);

        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        Assert.Null(result);
        Assert.Contains("Cover image exceeds", error);
    }

    [Fact]
    public async Task CreateAsync_WhenCoverUploadFails_CompensatesByDeletingPosterBlob()
    {
        using var context = CreateContext();
        var storage = MockStorage();
        // First call (poster, folder "event-posters") succeeds; second call (cover) throws
        storage.SetupSequence(m => m.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("event-posters/poster123.jpg")
            .ThrowsAsync(new Exception("Simulated cover upload failure"));

        var service = new EventSubmissionService(context, storage.Object, null);

        var (result, error) = await service.CreateAsync(ValidRequest(), Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal("Failed to upload event cover banner. Please try again.", error);
        storage.Verify(m => m.DeleteAsync("event-posters/poster123.jpg", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(context.Events); // nothing persisted
    }
}