namespace EventPulse.BookingService.DTOs;

public class CartOperationResult
{
    public bool Success { get; set; }
    public CartSummaryDto? Cart { get; set; }
    public string? Error { get; set; }
    public bool IsConflict { get; set; }
    public EventConflictDto? Conflict { get; set; }

    public static CartOperationResult Ok(CartSummaryDto cart) =>
        new() { Success = true, Cart = cart };

    public static CartOperationResult Fail(string error) =>
        new() { Success = false, Error = error };

    public static CartOperationResult EventConflict(EventConflictDto conflict) =>
        new() { Success = false, IsConflict = true, Conflict = conflict, Error = conflict.Message };
}
