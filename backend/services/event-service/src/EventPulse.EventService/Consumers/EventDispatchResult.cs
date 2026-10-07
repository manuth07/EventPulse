namespace EventPulse.EventService.Consumers;

public class EventDispatchResult
{
    public bool IsSuccess { get; set; }
    public bool IsRetryable { get; set; } = true;
    public string? FailureReason { get; set; }
    public string? ExceptionType { get; set; }

    public static EventDispatchResult Success() => new() { IsSuccess = true };

    public static EventDispatchResult NonRetryable(string reason, string? exceptionType = null) => new()
    {
        IsSuccess = false,
        IsRetryable = false,
        FailureReason = reason,
        ExceptionType = exceptionType
    };

    public static EventDispatchResult Retryable(string reason, string? exceptionType = null) => new()
    {
        IsSuccess = false,
        IsRetryable = true,
        FailureReason = reason,
        ExceptionType = exceptionType
    };

    public static implicit operator bool(EventDispatchResult result) => result?.IsSuccess ?? false;
}
