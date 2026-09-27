using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;

namespace EventPulse.BookingService.Services;

public class TicketCodeGenerator : ITicketCodeGenerator
{
    private readonly BookingDbContext _context;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public TicketCodeGenerator(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueTicketCodeAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;

        while (true)
        {
            var randomPart = GenerateRandomString(8);
            var code = $"EP-TKT-{year}-{randomPart}";

            var exists = await _context.Tickets
                .AnyAsync(t => t.TicketCode == code, cancellationToken);

            if (!exists)
            {
                return code;
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
