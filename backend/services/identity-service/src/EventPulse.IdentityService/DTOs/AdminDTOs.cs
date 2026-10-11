namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Safe summary representation of a registered user for administrative lists.
/// Sensitive credentials, security stamps, and password hashes are strictly excluded.
/// </summary>
public class AdminUserSummaryDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public string Role { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool ProfileCompleted { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Safe detailed representation of an individual user account for administrators.
/// </summary>
public class AdminUserDetailsDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public string Role { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool ProfileCompleted { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public AdminOrganizerApplicationSummaryDto? OrganizerApplication { get; set; }
}

/// <summary>
/// Summary of a user's organizer application attached to administrative user details.
/// </summary>
public class AdminOrganizerApplicationSummaryDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public string OrganizerType { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? ReviewComment { get; set; }
}

/// <summary>
/// Query filter and pagination parameters for administrative user searches.
/// </summary>
public class AdminUserQueryParameters
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 20 : (value > 100 ? 100 : value);
    }

    /// <summary>
    /// Optional search term for prefix/substring matching on name and email.
    /// </summary>
    public string? Q { get; set; }

    /// <summary>
    /// Optional role filter (All, Customer, Organizer, Administrator).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Optional status filter (All, Active, Inactive, Unverified).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Sort attribute (CreatedAt, LastName, FirstName, Email). Defaults to CreatedAt.
    /// </summary>
    public string? SortBy { get; set; } = "CreatedAt";

    /// <summary>
    /// Whether to sort descending (newest first). Defaults to true.
    /// </summary>
    public bool? SortDesc { get; set; } = true;
}

/// <summary>
/// Platform overview metrics approved in EP-368 with clear service ownership boundaries.
/// </summary>
public class AdminPlatformOverviewDto
{
    public UserMetricsDto Users { get; set; } = new();
    public PlatformDependencyMetricDto Events { get; set; } = new();
    public PlatformDependencyMetricDto Bookings { get; set; } = new();
}

/// <summary>
/// Identity Service domain user metrics.
/// </summary>
public class UserMetricsDto
{
    public string OwningService { get; set; } = "EventPulse.IdentityService";
    public int TotalUsers { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalOrganizers { get; set; }
    public int TotalAdministrators { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int PendingOrganizerApplications { get; set; }
}

/// <summary>
/// External microservice platform metric reporting authoritative ownership and availability.
/// If a downstream service is unreachable or not yet exposing aggregate metrics, the status
/// transparently reflects the dependency instead of returning fabricated values.
/// </summary>
public class PlatformDependencyMetricDto
{
    public string OwningService { get; set; } = string.Empty;
    public int? TotalCount { get; set; }
    public string Status { get; set; } = "Available"; // "Available", "Unavailable"
    public string? Message { get; set; }
}

/// <summary>
/// Request payload for activating or deactivating a user account.
/// </summary>
public class UserStatusChangeRequest
{
    public bool? IsActive { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Request payload for updating an account's assigned system role.
/// </summary>
public class UserRoleChangeRequest
{
    public string? Role { get; set; }
    public List<string>? Roles { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Structured outcome result returned by administrative actions.
/// </summary>
public class AdminActionResult
{
    public bool Succeeded { get; init; }
    public int StatusCode { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public AdminUserDetailsDto? User { get; init; }

    public static AdminActionResult Success(string message, AdminUserDetailsDto? user = null) =>
        new() { Succeeded = true, StatusCode = 200, Code = "SUCCESS", Message = message, User = user };

    public static AdminActionResult Failure(int statusCode, string code, string message) =>
        new() { Succeeded = false, StatusCode = statusCode, Code = code, Message = message };
}

