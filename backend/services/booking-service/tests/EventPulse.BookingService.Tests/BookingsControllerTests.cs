using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class BookingsControllerTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        
        return new BookingDbContext(options);
    }

    private BookingsController CreateController(
        BookingDbContext context, 
        Mock<IEventAvailabilityClient> eventClientMock, 
        Mock<IBookingReferenceGenerator> referenceGeneratorMock,
        Mock<IBookingEventPublisher> eventPublisherMock,
        Mock<ICartService> cartServiceMock,
        Mock<IBookingHistoryService>? bookingHistoryServiceMock = null,
        ClaimsPrincipal? user = null)
    {
        var loggerMock = new Mock<ILogger<BookingsController>>();
        var historyService = bookingHistoryServiceMock ?? new Mock<IBookingHistoryService>();
        var controller = new BookingsController(
            context,
            eventClientMock.Object,
            referenceGeneratorMock.Object,
            eventPublisherMock.Object,
            loggerMock.Object,
            cartServiceMock.Object,
            historyService.Object);

        // Mock User Context
        var effectiveUser = user ?? new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = effectiveUser }
        };

        return controller;
    }

    [Fact]
    public async Task CreateBooking_WithValidData_ShouldCreateBookingAndPublishEvent()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var referenceGeneratorMock = new Mock<IBookingReferenceGenerator>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var cartServiceMock = new Mock<ICartService>();

        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        eventClientMock.Setup(x => x.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Test Event" });

        eventClientMock.Setup(x => x.GetTicketTypeAsync(eventId, ticketTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTypeAvailability 
            { 
                Id = ticketTypeId, 
                Name = "VIP", 
                Price = 50.0m, 
                AvailableQuantity = 10, 
                IsSoldOut = false 
            });

        referenceGeneratorMock.Setup(x => x.GenerateUniqueReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("EP-2026-ABCDEF");

        var controller = CreateController(context, eventClientMock, referenceGeneratorMock, eventPublisherMock, cartServiceMock);

        var request = new CreateBookingRequest
        {
            EventId = eventId,
            Items = new List<CreateBookingItemRequest>
            {
                new CreateBookingItemRequest { TicketTypeId = ticketTypeId, Quantity = 2 }
            }
        };

        // Act
        var result = await controller.CreateBooking(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<BookingResponseDto>(createdResult.Value);

        Assert.Equal("EP-2026-ABCDEF", response.BookingReference);
        Assert.Equal(100.0m, response.TotalAmount); // 2 * 50 = 100
        Assert.Equal("PendingPayment", response.Status);
        
        eventPublisherMock.Verify(x => x.PublishBookingCreatedAsync(
            It.Is<BookingCreatedEvent>(e => e.TotalAmount == 100.0m && e.BookingReference == "EP-2026-ABCDEF"), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBooking_WithInsufficientCapacity_ShouldReturnBadRequest()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var referenceGeneratorMock = new Mock<IBookingReferenceGenerator>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var cartServiceMock = new Mock<ICartService>();

        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        eventClientMock.Setup(x => x.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Test Event" });

        eventClientMock.Setup(x => x.GetTicketTypeAsync(eventId, ticketTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTypeAvailability 
            { 
                Id = ticketTypeId, 
                Name = "VIP", 
                Price = 50.0m, 
                AvailableQuantity = 1, // Only 1 left
                IsSoldOut = false 
            });

        var controller = CreateController(context, eventClientMock, referenceGeneratorMock, eventPublisherMock, cartServiceMock);

        var request = new CreateBookingRequest
        {
            EventId = eventId,
            Items = new List<CreateBookingItemRequest>
            {
                new CreateBookingItemRequest { TicketTypeId = ticketTypeId, Quantity = 2 } // Wants 2
            }
        };

        // Act
        var result = await controller.CreateBooking(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        
        // Ensure booking was not saved
        var bookingCount = await context.Bookings.CountAsync();
        Assert.Equal(0, bookingCount);
        
        // Ensure event was not published
        eventPublisherMock.Verify(x => x.PublishBookingCreatedAsync(It.IsAny<BookingCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyBookings_WithoutValidIdentity_ReturnsUnauthorized()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var referenceGeneratorMock = new Mock<IBookingReferenceGenerator>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var cartServiceMock = new Mock<ICartService>();
        var historyServiceMock = new Mock<IBookingHistoryService>();

        // User without NameIdentifier or sub
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());

        var controller = CreateController(
            context,
            eventClientMock,
            referenceGeneratorMock,
            eventPublisherMock,
            cartServiceMock,
            historyServiceMock,
            unauthenticatedUser);

        var queryParams = new BookingHistoryQueryParameters();

        // Act
        var result = await controller.GetMyBookings(queryParams, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task GetMyBookings_WithValidIdentity_ReturnsOkWithPagedResult()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var eventClientMock = new Mock<IEventAvailabilityClient>();
        var referenceGeneratorMock = new Mock<IBookingReferenceGenerator>();
        var eventPublisherMock = new Mock<IBookingEventPublisher>();
        var cartServiceMock = new Mock<ICartService>();
        var historyServiceMock = new Mock<IBookingHistoryService>();

        var customerId = Guid.NewGuid();
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, customerId.ToString())
        }, "mock"));

        var queryParams = new BookingHistoryQueryParameters { Page = 1, PageSize = 10 };
        var expectedPagedResult = new PagedResult<CustomerBookingHistoryDto>
        {
            Items = new List<CustomerBookingHistoryDto>
            {
                new CustomerBookingHistoryDto(
                    Guid.NewGuid(),
                    "EP-2026-TEST1",
                    Guid.NewGuid(),
                    "Confirmed",
                    150.0m,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(3),
                    2,
                    new List<BookingHistoryItemDto>())
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        historyServiceMock.Setup(s => s.GetCustomerBookingHistoryAsync(customerId, queryParams, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPagedResult);

        var controller = CreateController(
            context,
            eventClientMock,
            referenceGeneratorMock,
            eventPublisherMock,
            cartServiceMock,
            historyServiceMock,
            authenticatedUser);

        // Act
        var result = await controller.GetMyBookings(queryParams, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerBookingHistoryDto>>(okResult.Value);
        Assert.Single(pagedResult.Items);
        Assert.Equal("EP-2026-TEST1", pagedResult.Items[0].BookingReference);
        Assert.Equal(1, pagedResult.TotalCount);
    }
}
