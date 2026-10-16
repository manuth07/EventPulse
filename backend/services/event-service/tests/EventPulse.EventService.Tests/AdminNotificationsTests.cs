using System.Reflection;
using System.Security.Claims;
using EventPulse.EventService;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Consumers;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class AdminNotificationsTests
{
    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_PersistsAdminNotification_WhenEventSubmittedReceived()
    {
        // Arrange
        using var context = CreateContext();
        var loggerMock = new Mock<ILogger<EventSubmittedNotificationHandler>>();
        var handler = new EventSubmittedNotificationHandler(context, loggerMock.Object);

        var messageId = Guid.NewGuid();
        var domainEventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var evt = new EventSubmittedEvent
        {
            EventMessageId = messageId,
            EventId = domainEventId,
            OrganizerId = organizerId,
            EventTitle = "Electronic Beats Gala",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        await handler.HandleAsync(evt);

        // Assert
        var notification = await context.AdminNotifications.FirstOrDefaultAsync(n => n.EventMessageId == messageId);
        Assert.NotNull(notification);
        Assert.Equal(domainEventId, notification.EventId);
        Assert.Equal("Electronic Beats Gala", notification.EventTitle);
        Assert.Equal(organizerId, notification.OrganizerId);
        Assert.False(notification.IsRead);
        Assert.Contains("Electronic Beats Gala", notification.Message);
        Assert.Equal($"/admin/events/pending/{domainEventId}", notification.NavigationTarget);
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_SkipsDuplicateOnRedelivery()
    {
        // Arrange
        using var context = CreateContext();
        var loggerMock = new Mock<ILogger<EventSubmittedNotificationHandler>>();
        var handler = new EventSubmittedNotificationHandler(context, loggerMock.Object);

        var messageId = Guid.NewGuid();
        var domainEventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var evt = new EventSubmittedEvent
        {
            EventMessageId = messageId,
            EventId = domainEventId,
            OrganizerId = organizerId,
            EventTitle = "Jazz and Blues Festival",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        // Act - 1st delivery
        await handler.HandleAsync(evt);

        // Act - 2nd delivery (redelivery of identical message)
        await handler.HandleAsync(evt);

        // Assert
        var count = await context.AdminNotifications.CountAsync(n => n.EventMessageId == messageId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsAllNotifications_OrderedNewestFirst_WithDynamicReviewStatus()
    {
        // Arrange
        using var context = CreateContext();
        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Event One",
            Description = "Desc",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(5),
            Price = 100,
            Status = EventStatus.Approved,
            OrganizerId = Guid.NewGuid()
        };
        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Event Two",
            Description = "Desc",
            Venue = "Venue",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 200,
            Status = EventStatus.Pending,
            OrganizerId = Guid.NewGuid()
        };

        context.Events.AddRange(event1, event2);

        var notif1 = new AdminNotification
        {
            Id = Guid.NewGuid(),
            EventMessageId = Guid.NewGuid(),
            EventId = event1.Id,
            EventTitle = event1.Title,
            OrganizerId = event1.OrganizerId,
            Message = "New event submitted: Event One",
            NavigationTarget = $"/admin/events/pending/{event1.Id}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            IsRead = true
        };

        var notif2 = new AdminNotification
        {
            Id = Guid.NewGuid(),
            EventMessageId = Guid.NewGuid(),
            EventId = event2.Id,
            EventTitle = event2.Title,
            OrganizerId = event2.OrganizerId,
            Message = "New event submitted: Event Two",
            NavigationTarget = $"/admin/events/pending/{event2.Id}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            IsRead = false
        };

        context.AdminNotifications.AddRange(notif1, notif2);
        await context.SaveChangesAsync();

        var service = new AdminNotificationService(context);

        // Act
        var result = await service.GetNotificationsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        // Ordered newest first (notif2 before notif1)
        Assert.Equal(notif2.Id, result[0].Id);
        Assert.Equal("Pending", result[0].ReviewStatus);
        Assert.False(result[0].IsRead);

        Assert.Equal(notif1.Id, result[1].Id);
        Assert.Equal("Approved", result[1].ReviewStatus);
        Assert.True(result[1].IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var context = CreateContext();
        context.AdminNotifications.AddRange(
            new AdminNotification
            {
                Id = Guid.NewGuid(),
                EventMessageId = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventTitle = "A",
                Message = "Msg",
                NavigationTarget = "/test",
                IsRead = false
            },
            new AdminNotification
            {
                Id = Guid.NewGuid(),
                EventMessageId = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventTitle = "B",
                Message = "Msg",
                NavigationTarget = "/test",
                IsRead = false
            },
            new AdminNotification
            {
                Id = Guid.NewGuid(),
                EventMessageId = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventTitle = "C",
                Message = "Msg",
                NavigationTarget = "/test",
                IsRead = true
            }
        );
        await context.SaveChangesAsync();

        var service = new AdminNotificationService(context);

        // Act
        var count = await service.GetUnreadCountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesIsReadAndTimestamp_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContext();
        var notif = new AdminNotification
        {
            Id = Guid.NewGuid(),
            EventMessageId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventTitle = "A",
            Message = "Msg",
            NavigationTarget = "/test",
            IsRead = false
        };
        context.AdminNotifications.Add(notif);
        await context.SaveChangesAsync();

        var service = new AdminNotificationService(context);

        // Act
        var success = await service.MarkAsReadAsync(notif.Id);

        // Assert
        Assert.True(success);
        var updated = await context.AdminNotifications.FindAsync(notif.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAsReadAsync_ReturnsFalse_WhenNotificationNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var service = new AdminNotificationService(context);

        // Act
        var success = await service.MarkAsReadAsync(Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task Controller_GetNotifications_ReturnsOkWithList()
    {
        // Arrange
        var mockService = new Mock<IAdminNotificationService>();
        var list = new List<AdminNotificationDto>
        {
            new() { Id = Guid.NewGuid(), EventTitle = "Rock Fest", ReviewStatus = "Pending" }
        };
        mockService.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var controller = new AdminNotificationsController(mockService.Object, Mock.Of<ILogger<AdminNotificationsController>>());

        // Act
        var result = await controller.GetNotifications(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AdminNotificationDto>>(okResult.Value);
        Assert.Single(items);
        Assert.Equal("Rock Fest", items[0].EventTitle);
    }

    [Fact]
    public async Task Controller_GetUnreadCount_ReturnsOkWithCount()
    {
        // Arrange
        var mockService = new Mock<IAdminNotificationService>();
        mockService.Setup(s => s.GetUnreadCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var controller = new AdminNotificationsController(mockService.Object, Mock.Of<ILogger<AdminNotificationsController>>());

        // Act
        var result = await controller.GetUnreadCount(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var count = Assert.IsType<int>(okResult.Value);
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task Controller_MarkAsRead_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var notifId = Guid.NewGuid();
        var mockService = new Mock<IAdminNotificationService>();
        mockService.Setup(s => s.MarkAsReadAsync(notifId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new AdminNotificationsController(mockService.Object, Mock.Of<ILogger<AdminNotificationsController>>());

        // Act
        var result = await controller.MarkAsRead(notifId.ToString(), CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Controller_MarkAsRead_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var notifId = Guid.NewGuid();
        var mockService = new Mock<IAdminNotificationService>();
        mockService.Setup(s => s.MarkAsReadAsync(notifId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new AdminNotificationsController(mockService.Object, Mock.Of<ILogger<AdminNotificationsController>>());

        // Act
        var result = await controller.MarkAsRead(notifId.ToString(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Controller_MarkAsRead_ReturnsBadRequest_WhenGuidMalformed()
    {
        // Arrange
        var mockService = new Mock<IAdminNotificationService>();
        var controller = new AdminNotificationsController(mockService.Object, Mock.Of<ILogger<AdminNotificationsController>>());

        // Act
        var result = await controller.MarkAsRead("not-a-valid-guid", CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Controller_HasAdministratorOnlyPolicyAuthorization()
    {
        // Verify controller type is guarded by [Authorize(Policy = AppPolicies.AdministratorOnly)]
        var controllerType = typeof(AdminNotificationsController);
        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
        Assert.Equal(AppPolicies.AdministratorOnly, authAttr.Policy);
    }
}
