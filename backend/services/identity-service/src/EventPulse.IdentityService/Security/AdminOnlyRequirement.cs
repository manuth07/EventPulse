using Microsoft.AspNetCore.Authorization;

namespace EventPulse.IdentityService.Security;

/// <summary>
/// Authorization requirement enforcing active Administrator status.
/// Handled by <see cref="AdminOnlyAuthorizationHandler"/>, which validates both
/// the JWT role claim and live database status to protect against stale tokens.
/// </summary>
public class AdminOnlyRequirement : IAuthorizationRequirement
{
}
