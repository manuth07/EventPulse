using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class BookingReferenceGeneratorTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task GenerateUniqueReferenceAsync_ShouldMatchFormat()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var generator = new BookingReferenceGenerator(context);
        
        // Act
        var reference = await generator.GenerateUniqueReferenceAsync();

        // Assert
        Assert.NotNull(reference);
        
        // Format should be EP-{YYYY}-{6_CHARS}
        var year = DateTime.UtcNow.Year;
        var regex = new Regex($@"^EP-{year}-[A-Z2-9]{{6}}$");
        Assert.Matches(regex, reference);
    }

    [Fact]
    public async Task GenerateUniqueReferenceAsync_ShouldEnsureUniqueness()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        
        // To properly test uniqueness collisions we would ideally mock the random generator.
        // But since we can't easily do that here without changing the design, we'll just test
        // generating multiple and ensuring they are different.
        var generator = new BookingReferenceGenerator(context);
        
        // Act
        var reference1 = await generator.GenerateUniqueReferenceAsync();
        
        // Save to DB to ensure the generator checks against it
        context.Bookings.Add(new Booking { 
            BookingReference = reference1, 
            TotalAmount = 100, 
            CustomerId = Guid.NewGuid(), 
            EventId = Guid.NewGuid() 
        });
        await context.SaveChangesAsync();

        var reference2 = await generator.GenerateUniqueReferenceAsync();

        // Assert
        Assert.NotEqual(reference1, reference2);
    }
}
