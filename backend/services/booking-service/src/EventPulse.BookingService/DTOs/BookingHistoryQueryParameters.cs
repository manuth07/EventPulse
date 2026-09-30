namespace EventPulse.BookingService.DTOs;

public class BookingHistoryQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;

    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 1 : value);
    }
}
