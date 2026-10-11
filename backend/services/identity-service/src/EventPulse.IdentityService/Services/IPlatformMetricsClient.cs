using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Fetches external microservice platform metrics respecting service boundaries.
/// Never accesses other service databases directly.
/// </summary>
public interface IPlatformMetricsClient
{
    Task<PlatformDependencyMetricDto> GetEventsMetricAsync(CancellationToken cancellationToken = default);
    Task<PlatformDependencyMetricDto> GetBookingsMetricAsync(CancellationToken cancellationToken = default);
}
