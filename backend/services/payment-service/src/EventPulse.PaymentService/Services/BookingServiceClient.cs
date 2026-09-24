using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventPulse.PaymentService.DTOs;

namespace EventPulse.PaymentService.Services;

public class BookingServiceClient : IBookingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BookingServiceClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BookingServiceClient(HttpClient httpClient, ILogger<BookingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookingSummaryDto?> GetBookingSummaryAsync(Guid bookingId, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/bookings/{bookingId}/summary");
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BookingService responded with status {StatusCode} for booking {BookingId}", response.StatusCode, bookingId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<BookingSummaryDto>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call BookingService for booking {BookingId}", bookingId);
            return null;
        }
    }
}
