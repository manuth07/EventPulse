using System.Net.Http.Json;

namespace EventPulse.BookingService.Services;

public class EventServiceAvailabilityClient : IEventAvailabilityClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EventServiceAvailabilityClient>? _logger;

    public EventServiceAvailabilityClient(HttpClient httpClient, ILogger<EventServiceAvailabilityClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TicketTypeAvailability?> GetTicketTypeAsync(
        Guid eventId, Guid ticketTypeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/events/{eventId}/ticket-types/public", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var ticketTypes = await response.Content.ReadFromJsonAsync<List<TicketTypeAvailability>>(cancellationToken: cancellationToken);
            return ticketTypes?.FirstOrDefault(t => t.Id == ticketTypeId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch ticket availability for TicketType {TicketTypeId}", ticketTypeId);
            return null;
        }
    }

    public async Task<EventSummaryInfo?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/events/{eventId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<EventSummaryInfo>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch event summary for Event {EventId}", eventId);
            return null;
        }
    }
}