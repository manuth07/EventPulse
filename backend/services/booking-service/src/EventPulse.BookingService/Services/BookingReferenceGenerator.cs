using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;

namespace EventPulse.BookingService.Services;

public class BookingReferenceGenerator : IBookingReferenceGenerator
{
    private readonly BookingDbContext _context;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed similar looking characters like O, 0, 1, I

    public BookingReferenceGenerator(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueReferenceAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        
        while (true)
        {
            var randomString = GenerateRandomString(6);
            var reference = $"EP-{year}-{randomString}";

            // Check against database to guarantee uniqueness
            var exists = await _context.Bookings
                .AnyAsync(b => b.BookingReference == reference, cancellationToken);

            if (!exists)
            {
                return reference;
            }
        }
    }

    private static string GenerateRandomString(int length)
    {
        var chars = new char[length];
        var bytes = new byte[length];
        
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
