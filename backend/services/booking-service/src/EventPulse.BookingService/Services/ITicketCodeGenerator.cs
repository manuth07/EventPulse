namespace EventPulse.BookingService.Services;

public interface ITicketCodeGenerator
{
    Task<string> GenerateUniqueTicketCodeAsync(CancellationToken cancellationToken = default);
}
