using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;

namespace EventPulse.BookingService.Services;

public class ValidationTokenGenerator : IValidationTokenGenerator
{
    private readonly BookingDbContext _context;

    public ValidationTokenGenerator(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueValidationTokenAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

            var exists = await _context.Tickets
                .AnyAsync(t => t.ValidationToken == token, cancellationToken);

            if (!exists)
            {
                return token;
            }
        }
    }
}
