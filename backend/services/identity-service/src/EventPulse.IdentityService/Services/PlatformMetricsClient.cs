using System.Text.Json;
using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Default implementation of IPlatformMetricsClient.
/// Communicates with downstream microservices via HTTP APIs and returns structured
/// dependency metrics, reporting unavailable status if endpoints are unreachable.
/// </summary>
public class PlatformMetricsClient : IPlatformMetricsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformMetricsClient> _logger;

    public PlatformMetricsClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PlatformMetricsClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PlatformDependencyMetricDto> GetEventsMetricAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["EventService:BaseUrl"] ?? "http://localhost:7102";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/events", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return new PlatformDependencyMetricDto
                    {
                        OwningService = "EventPulse.EventService",
                        TotalCount = doc.RootElement.GetArrayLength(),
                        Status = "Available",
                        Message = "Retrieved from Event Service"
                    };
                }
            }

            return new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.EventService",
                TotalCount = null,
                Status = "Unavailable",
                Message = $"Event Service returned HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Event Service metrics from {BaseUrl}", baseUrl);
            return new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.EventService",
                TotalCount = null,
                Status = "Unavailable",
                Message = "Event Service is unreachable or endpoint not available"
            };
        }
    }

    public async Task<PlatformDependencyMetricDto> GetBookingsMetricAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["BookingService:BaseUrl"] ?? "http://localhost:7103";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/bookings/admin/count", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (int.TryParse(content, out var count))
                {
                    return new PlatformDependencyMetricDto
                    {
                        OwningService = "EventPulse.BookingService",
                        TotalCount = count,
                        Status = "Available",
                        Message = "Retrieved from Booking Service"
                    };
                }
            }

            return new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.BookingService",
                TotalCount = null,
                Status = "Unavailable",
                Message = "Booking Service metrics endpoint not available"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Booking Service metrics from {BaseUrl}", baseUrl);
            return new PlatformDependencyMetricDto
            {
                OwningService = "EventPulse.BookingService",
                TotalCount = null,
                Status = "Unavailable",
                Message = "Booking Service is unreachable or endpoint not available"
            };
        }
    }
}
