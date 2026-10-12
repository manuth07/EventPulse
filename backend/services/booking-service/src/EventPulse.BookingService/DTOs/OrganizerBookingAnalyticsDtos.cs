namespace EventPulse.BookingService.DTOs;

public class OrganizerBookingSummaryDto
{
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int TotalTicketsBooked { get; set; }
    public int ActiveTicketsBooked { get; set; }
    public int CancelledTickets { get; set; }
    public decimal CancellationRate { get; set; }
    public decimal TicketCancellationRate { get; set; }
    public decimal AverageTicketsPerBooking { get; set; }
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
}

public class TicketTypeBookingSummaryDto
{
    public Guid TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public int BookingsCount { get; set; }
    public int TicketsBooked { get; set; }
    public int ActiveTickets { get; set; }
    public int CancelledTickets { get; set; }
    public int Capacity { get; set; }
    public int RemainingCapacity { get; set; }
}

public class EventBookingAnalyticsDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int TotalTicketsBooked { get; set; }
    public int ActiveTicketsBooked { get; set; }
    public int CancelledTickets { get; set; }
    public decimal CancellationRate { get; set; }
    public decimal TicketCancellationRate { get; set; }
    public int TotalCapacity { get; set; }
    public int RemainingCapacity { get; set; }
    public decimal CapacityUtilizationRate { get; set; }
    public ReportingPeriodDto ReportingPeriod { get; set; } = new();
    public List<TicketTypeBookingSummaryDto> TicketTypes { get; set; } = new();
}

public class EventBookingSummaryItemDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int TotalTicketsBooked { get; set; }
    public int ActiveTicketsBooked { get; set; }
    public decimal CancellationRate { get; set; }
    public int TotalCapacity { get; set; }
    public int RemainingCapacity { get; set; }
    public DateTimeOffset? LastBookingAtUtc { get; set; }
}
