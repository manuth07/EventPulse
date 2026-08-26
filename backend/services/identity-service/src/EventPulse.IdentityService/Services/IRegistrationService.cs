using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

public interface IRegistrationService
{
    Task<RegistrationResult> RegisterCustomerAsync(RegisterRequest request);
}
