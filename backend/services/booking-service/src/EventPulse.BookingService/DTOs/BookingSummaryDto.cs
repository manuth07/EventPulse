namespace EventPulse.BookingService.DTOs;

public record BookingSummaryDto(
    Guid Id,
    string BookingReference,
    Guid CustomerId,
    decimal TotalAmount,
    string Status
);
