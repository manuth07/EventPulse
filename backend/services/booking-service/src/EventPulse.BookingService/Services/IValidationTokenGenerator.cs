namespace EventPulse.BookingService.Services;

public interface IValidationTokenGenerator
{
    Task<string> GenerateUniqueValidationTokenAsync(CancellationToken cancellationToken = default);
}
