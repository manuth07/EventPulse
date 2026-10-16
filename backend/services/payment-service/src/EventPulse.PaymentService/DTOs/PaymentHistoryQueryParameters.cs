namespace EventPulse.PaymentService.DTOs;

public class PaymentHistoryQueryParameters
{
    private const int MaxPageSize = 50;
    private int _page = 1;
    private int _pageSize = 10;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : (value > MaxPageSize ? MaxPageSize : value);
    }

    public string? Status { get; set; }
}
