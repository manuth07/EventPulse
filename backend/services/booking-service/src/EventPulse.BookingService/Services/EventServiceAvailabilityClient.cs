using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task<IReadOnlyList<TicketTypeAvailability>> GetEventTicketTypesAsync(
        Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/events/{eventId}/ticket-types/public", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<TicketTypeAvailability>();

            var ticketTypes = await response.Content.ReadFromJsonAsync<List<TicketTypeAvailability>>(cancellationToken: cancellationToken);
            return ticketTypes ?? (IReadOnlyList<TicketTypeAvailability>)Array.Empty<TicketTypeAvailability>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch ticket types for Event {EventId}", eventId);
            return Array.Empty<TicketTypeAvailability>();
        }
    }

    public async Task<EventSummaryInfo?> GetEventSummaryAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try internal endpoint first (returns event regardless of public lifecycle status)
            var response = await _httpClient.GetAsync($"/api/events/internal/{eventId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to public endpoint
                response = await _httpClient.GetAsync($"/api/events/{eventId}", cancellationToken);
            }

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

    public async Task<IReadOnlyList<EventSummaryInfo>> GetOrganizerEventsAsync(Guid organizerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/events/internal/organizer/{organizerId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<EventSummaryInfo>();

            var events = await response.Content.ReadFromJsonAsync<List<EventSummaryInfo>>(cancellationToken: cancellationToken);
            return events ?? (IReadOnlyList<EventSummaryInfo>)Array.Empty<EventSummaryInfo>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch events for Organizer {OrganizerId}", organizerId);
            return Array.Empty<EventSummaryInfo>();
        }
    }

    public async Task<ClientInventoryResult> ReserveInventoryAsync(
        Guid eventId,
        Guid bookingId,
        IEnumerable<ReserveInventoryItemDto> items,
        int holdDurationMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                bookingId,
                holdDurationMinutes,
                items = items.Select(i => new { ticketTypeId = i.TicketTypeId, quantity = i.Quantity })
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/inventory/reserve", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ClientInventoryResult.Ok();

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            string message = "Failed to reserve inventory.";
            try
            {
                using var doc = JsonDocument.Parse(errorBody);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString() ?? message;
                }
            }
            catch { }

            return ClientInventoryResult.Fail(message, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling EventService reserve inventory for Event {EventId}, Booking {BookingId}", eventId, bookingId);
            return ClientInventoryResult.Fail($"Network error reserving inventory: {ex.Message}", 500);
        }
    }

    public async Task<ClientInventoryResult> CommitInventoryAsync(
        Guid eventId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { bookingId };
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/inventory/commit", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ClientInventoryResult.Ok();

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            string message = "Failed to commit inventory.";
            try
            {
                using var doc = JsonDocument.Parse(errorBody);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString() ?? message;
                }
            }
            catch { }

            return ClientInventoryResult.Fail(message, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling EventService commit inventory for Event {EventId}, Booking {BookingId}", eventId, bookingId);
            return ClientInventoryResult.Fail($"Network error committing inventory: {ex.Message}", 500);
        }
    }

    public async Task<ClientInventoryResult> ReleaseInventoryAsync(
        Guid eventId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { bookingId };
            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/inventory/release", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ClientInventoryResult.Ok();

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ClientInventoryResult.Fail(errorBody, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling EventService release inventory for Event {EventId}, Booking {BookingId}", eventId, bookingId);
            return ClientInventoryResult.Fail($"Network error releasing inventory: {ex.Message}", 500);
        }
    }

    public async Task<ClientInventoryResult> RestoreInventoryAsync(
        Guid eventId,
        Guid bookingId,
        string cancellationKey,
        IEnumerable<ReserveInventoryItemDto> items,
        Guid? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                bookingId,
                cancellationKey,
                ticketId,
                items = items.Select(i => new { ticketTypeId = i.TicketTypeId, quantity = i.Quantity })
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/inventory/restore", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ClientInventoryResult.Ok();

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ClientInventoryResult.Fail(errorBody, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling EventService restore inventory for Event {EventId}, Booking {BookingId}", eventId, bookingId);
            return ClientInventoryResult.Fail($"Network error restoring inventory: {ex.Message}", 500);
        }
    }
}