using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Describes the result of a registration attempt.
/// </summary>
public class RegistrationResult
{
    public bool Succeeded { get; init; }
    public bool IsDuplicateEmail { get; init; }
    public RegisterResponse? Response { get; init; }
    public IEnumerable<string> Errors { get; init; } = [];

    public static RegistrationResult Success(RegisterResponse response) =>
        new() { Succeeded = true, Response = response };

    public static RegistrationResult Duplicate() =>
        new() { Succeeded = false, IsDuplicateEmail = true };

    public static RegistrationResult Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
