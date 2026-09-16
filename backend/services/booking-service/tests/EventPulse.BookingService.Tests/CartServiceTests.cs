using Microsoft.EntityFrameworkCore;
using Moq;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;

namespace EventPulse.BookingService.Tests;

public class CartServiceTests : IDisposable
{
    private readonly BookingDbContext _context;
    private readonly Mock<IEventAvailabilityClient> _mockAvailabilityClient;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: $"BookingTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new BookingDbContext(options);
        _mockAvailabilityClient = new Mock<IEventAvailabilityClient>();
        _cartService = new CartService(_context, _mockAvailabilityClient.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SetupTicketType(Guid eventId, Guid ticketTypeId, string name, decimal price, int available, bool isSoldOut = false)
    {
        _mockAvailabilityClient
            .Setup(c => c.GetTicketTypeAsync(eventId, ticketTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTypeAvailability
            {
                Id = ticketTypeId,
                Name = name,
                Price = price,
                AvailableQuantity = available,
                IsSoldOut = isSoldOut
            });

        _mockAvailabilityClient
            .Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Test Event",
                Venue = "Test Venue",
                EventDate = DateTime.UtcNow.AddDays(10),
                Status = "Published"
            });
    }

    [Fact]
    public async Task AddToCart_CreatesActiveCart_WithSingleEventAndCorrectTotals()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "VIP", 5000m, 20);

        var request = new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2
        };

        var result = await _cartService.AddOrUpdateItemAsync(customerId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Cart);
        Assert.Equal(eventId, result.Cart.EventId);
        Assert.Equal(1, result.Cart.Items.Count);
        Assert.Equal(2, result.Cart.TotalTicketCount);
        Assert.Equal(10000m, result.Cart.TotalAmount);
        Assert.Equal("VIP", result.Cart.Items[0].TicketTypeName);

        var dbCart = await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CustomerId == customerId);
        Assert.NotNull(dbCart);
        Assert.Equal(CartStatus.Active, dbCart.Status);
        Assert.Equal(eventId, dbCart.EventId);
    }

    [Fact]
    public async Task AddToCart_UpdatesQuantityIdempotently_WhenItemAlreadyExists()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "VIP", 5000m, 20);

        // First selection: 2 VIP
        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2
        });

        // Second call with desired final quantity = 3 (does NOT add 2+3=5, but sets to 3)
        var result = await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 3
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.Cart!.Items.Count);
        Assert.Equal(3, result.Cart.TotalTicketCount);
        Assert.Equal(15000m, result.Cart.TotalAmount);

        var dbItems = await _context.CartItems.Where(i => i.CustomerId == customerId).ToListAsync();
        Assert.Single(dbItems);
        Assert.Equal(3, dbItems[0].Quantity);
    }

    [Fact]
    public async Task AddToCart_RejectsDifferentEvent_WithEventConflict()
    {
        var customerId = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var ticketA = Guid.NewGuid();
        var ticketB = Guid.NewGuid();

        SetupTicketType(eventA, ticketA, "General", 1000m, 50);
        SetupTicketType(eventB, ticketB, "VIP", 5000m, 20);

        // Add tickets for Event A
        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventA,
            TicketTypeId = ticketA,
            Quantity = 2
        });

        // Attempt to add tickets for Event B without ClearExisting
        var result = await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventB,
            TicketTypeId = ticketB,
            Quantity = 1,
            ClearExisting = false
        });

        Assert.False(result.Success);
        Assert.True(result.IsConflict);
        Assert.NotNull(result.Conflict);
        Assert.Equal(eventA, result.Conflict.CurrentEventId);
        Assert.Equal(eventB, result.Conflict.AttemptedEventId);

        // Ensure Event A cart is intact
        var currentCart = await _cartService.GetActiveCartAsync(customerId);
        Assert.Equal(eventA, currentCart.EventId);
        Assert.Equal(2, currentCart.TotalTicketCount);
    }

    [Fact]
    public async Task AddToCart_ClearsExistingCart_WhenClearExistingIsTrue()
    {
        var customerId = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var ticketA = Guid.NewGuid();
        var ticketB = Guid.NewGuid();

        SetupTicketType(eventA, ticketA, "General", 1000m, 50);
        SetupTicketType(eventB, ticketB, "VIP", 5000m, 20);

        // Add tickets for Event A
        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventA,
            TicketTypeId = ticketA,
            Quantity = 2
        });

        // Add tickets for Event B with ClearExisting = true
        var result = await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventB,
            TicketTypeId = ticketB,
            Quantity = 1,
            ClearExisting = true
        });

        Assert.True(result.Success);
        Assert.Equal(eventB, result.Cart!.EventId);
        Assert.Equal(1, result.Cart.TotalTicketCount);
        Assert.Equal(5000m, result.Cart.TotalAmount);

        // Verify that Event A cart was abandoned and Event B cart is the only Active one
        var activeCarts = await _context.Carts.Where(c => c.CustomerId == customerId && c.Status == CartStatus.Active).ToListAsync();
        Assert.Single(activeCarts);
        Assert.Equal(eventB, activeCarts[0].EventId);
    }

    [Fact]
    public async Task AddToCart_EnforcesAvailabilityLimit()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "VIP", 5000m, available: 3);

        var result = await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 4 // Exceeds available 3
        });

        Assert.False(result.Success);
        Assert.Contains("Only 3 ticket(s) available", result.Error);
    }

    [Fact]
    public async Task AddToCart_RejectsSoldOutTicket()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "VIP", 5000m, available: 0, isSoldOut: true);

        var result = await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1
        });

        Assert.False(result.Success);
        Assert.Contains("sold out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetItemQuantity_UpdatesQuantityAndTotal()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "General", 1000m, 50);

        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2
        });

        var (cart, error) = await _cartService.SetItemQuantityAsync(customerId, ticketTypeId, 4);

        Assert.Null(error);
        Assert.NotNull(cart);
        Assert.Equal(4, cart.TotalTicketCount);
        Assert.Equal(4000m, cart.TotalAmount);
    }

    [Fact]
    public async Task SetItemQuantity_RemovesItem_WhenQuantityIsZero()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "General", 1000m, 50);

        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2
        });

        var (cart, error) = await _cartService.SetItemQuantityAsync(customerId, ticketTypeId, 0);

        Assert.Null(error);
        Assert.NotNull(cart);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalTicketCount);
        Assert.Equal(0m, cart.TotalAmount);
    }

    [Fact]
    public async Task RemoveItem_RemovesSpecificItemFromCart()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketA = Guid.NewGuid();
        var ticketB = Guid.NewGuid();

        SetupTicketType(eventId, ticketA, "General", 1000m, 50);
        SetupTicketType(eventId, ticketB, "VIP", 5000m, 20);

        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest { EventId = eventId, TicketTypeId = ticketA, Quantity = 2 });
        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest { EventId = eventId, TicketTypeId = ticketB, Quantity = 1 });

        var (cart, error) = await _cartService.RemoveItemAsync(customerId, ticketA);

        Assert.Null(error);
        Assert.NotNull(cart);
        Assert.Single(cart.Items);
        Assert.Equal(ticketB, cart.Items[0].TicketTypeId);
        Assert.Equal(1, cart.TotalTicketCount);
        Assert.Equal(5000m, cart.TotalAmount);
    }

    [Fact]
    public async Task ClearActiveCart_AbandonsCartAndClearsItems()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "General", 1000m, 50);

        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 3
        });

        var cleared = await _cartService.ClearActiveCartAsync(customerId);
        Assert.True(cleared);

        var activeCart = await _cartService.GetActiveCartAsync(customerId);
        Assert.Empty(activeCart.Items);
        Assert.Equal(0, activeCart.TotalTicketCount);
    }

    [Fact]
    public async Task CompleteActiveCart_MarksCartCompleted_AndDoesNotReturnItAsActive()
    {
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        SetupTicketType(eventId, ticketTypeId, "VIP", 5000m, 20);

        await _cartService.AddOrUpdateItemAsync(customerId, new AddToCartRequest
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2
        });

        var completed = await _cartService.CompleteActiveCartAsync(customerId);
        Assert.True(completed);

        // Verify DB has cart marked Completed
        var dbCart = await _context.Carts.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        Assert.NotNull(dbCart);
        Assert.Equal(CartStatus.Completed, dbCart.Status);

        // GET active cart returns empty summary
        var activeCart = await _cartService.GetActiveCartAsync(customerId);
        Assert.Empty(activeCart.Items);
        Assert.Equal(0, activeCart.TotalTicketCount);
    }
}
