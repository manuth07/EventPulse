namespace EventPulse.EventService.DTOs;

public class ReserveInventoryRequest
{
    public Guid BookingId { get; set; }
    public List<TicketReservationItemDto> Items { get; set; } = new();
    public int HoldDurationMinutes { get; set; } = 15;
}

public class TicketReservationItemDto
{
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
}

public class CommitInventoryRequest
{
    public Guid BookingId { get; set; }
}

public class ReleaseInventoryRequest
{
    public Guid BookingId { get; set; }
}

public class RestoreInventoryRequest
{
    public Guid BookingId { get; set; }
    public string CancellationKey { get; set; } = string.Empty;
    public Guid? TicketId { get; set; }
    public List<TicketReservationItemDto> Items { get; set; } = new();
}

public class InventoryOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; } = 200;
    public List<TicketTypeInventoryStatusDto> TicketStatuses { get; set; } = new();

    public static InventoryOperationResult Succeeded(List<TicketTypeInventoryStatusDto>? statuses = null) =>
        new() { Success = true, StatusCode = 200, TicketStatuses = statuses ?? new() };

    public static InventoryOperationResult Failed(string message, int statusCode = 400) =>
        new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}

public class TicketTypeInventoryStatusDto
{
    public Guid TicketTypeId { get; set; }
    public int TotalQuantity { get; set; }
    public int SoldQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}
