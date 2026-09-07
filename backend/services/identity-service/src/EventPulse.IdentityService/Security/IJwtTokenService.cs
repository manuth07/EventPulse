using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Security;

public interface IJwtTokenService
{
    (string Token, int ExpiresInSeconds) GenerateToken(ApplicationUser user, IList<string> roles);
}
