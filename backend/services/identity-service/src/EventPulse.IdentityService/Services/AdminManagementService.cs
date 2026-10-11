using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Implements administrative management operations for querying users, details, and platform metrics.
/// </summary>
public class AdminManagementService : IAdminManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IPlatformMetricsClient _metricsClient;
    private readonly ILogger<AdminManagementService> _logger;

    public AdminManagementService(
        ApplicationDbContext context,
        IPlatformMetricsClient metricsClient,
        ILogger<AdminManagementService> logger)
    {
        _context = context;
        _metricsClient = metricsClient;
        _logger = logger;
    }

    public async Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        AdminUserQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = _context.Users.AsNoTracking();

        // 1. Search Query Filter (name or email)
        if (!string.IsNullOrWhiteSpace(parameters.Q))
        {
            var q = parameters.Q.Trim().ToLower();
            usersQuery = usersQuery.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(q)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(q)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(q)) ||
                ((u.FirstName + " " + u.LastName).ToLower().Contains(q)));
        }

        // 2. Account Status Filter
        if (!string.IsNullOrWhiteSpace(parameters.Status) && !parameters.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var status = parameters.Status.Trim();
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                usersQuery = usersQuery.Where(u => u.IsActive);
            }
            else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                usersQuery = usersQuery.Where(u => !u.IsActive);
            }
            else if (status.Equals("Unverified", StringComparison.OrdinalIgnoreCase))
            {
                usersQuery = usersQuery.Where(u => !u.EmailConfirmed);
            }
        }

        // 3. Role Filter
        if (!string.IsNullOrWhiteSpace(parameters.Role) && !parameters.Role.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var targetRole = parameters.Role.Trim();
            var normalizedTargetRole = targetRole.ToUpperInvariant();

            var roleUsersQuery = from ur in _context.UserRoles
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 where r.NormalizedName == normalizedTargetRole || (r.Name != null && r.Name.ToUpper() == normalizedTargetRole)
                                 select ur.UserId;

            if (normalizedTargetRole == AppRoles.Customer.ToUpperInvariant())
            {
                // Customer: either explicitly in Customer role OR no roles in UserRoles
                var nonCustomerUserIds = from ur in _context.UserRoles
                                         join r in _context.Roles on ur.RoleId equals r.Id
                                         where r.NormalizedName != normalizedTargetRole && (r.Name != null && r.Name.ToUpper() != normalizedTargetRole)
                                         select ur.UserId;

                usersQuery = usersQuery.Where(u => roleUsersQuery.Contains(u.Id) || !nonCustomerUserIds.Contains(u.Id));
            }
            else
            {
                usersQuery = usersQuery.Where(u => roleUsersQuery.Contains(u.Id));
            }
        }

        // 4. Deterministic Ordering
        var sortBy = (parameters.SortBy ?? "CreatedAt").Trim().ToLowerInvariant();
        bool sortDesc = parameters.SortDesc ?? true;

        usersQuery = (sortBy, sortDesc) switch
        {
            ("lastname", true) => usersQuery.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName).ThenByDescending(u => u.Id),
            ("lastname", false) => usersQuery.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id),
            ("firstname", true) => usersQuery.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName).ThenByDescending(u => u.Id),
            ("firstname", false) => usersQuery.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),
            ("email", true) => usersQuery.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
            ("email", false) => usersQuery.OrderBy(u => u.Email).ThenBy(u => u.Id),
            (_, false) => usersQuery.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            _ => usersQuery.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
        };

        // 5. Total Count and Pagination
        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var page = parameters.Page;
        var pageSize = parameters.PageSize;

        var pagedUsers = await usersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 6. Role Resolution
        var userIds = pagedUsers.Select(u => u.Id).ToList();
        var userRolesList = await (from ur in _context.UserRoles
                                   where userIds.Contains(ur.UserId)
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, RoleName = r.Name })
                                   .ToListAsync(cancellationToken);

        var rolesByUser = userRolesList
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.RoleName!).Where(r => !string.IsNullOrEmpty(r)).ToList()
            );

        // 7. Mapping to Safe Summary DTOs
        var items = pagedUsers.Select(u =>
        {
            var roles = rolesByUser.TryGetValue(u.Id, out var rList) && rList.Count > 0
                ? rList
                : new[] { AppRoles.Customer };

            var primaryRole = roles.Contains(AppRoles.Administrator)
                ? AppRoles.Administrator
                : (roles.Contains(AppRoles.Organizer) ? AppRoles.Organizer : AppRoles.Customer);

            return new AdminUserSummaryDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                CountryCode = u.CountryCode,
                Role = primaryRole,
                Roles = roles,
                IsActive = u.IsActive,
                EmailConfirmed = u.EmailConfirmed,
                ProfileCompleted = u.ProfileCompleted,
                ProfilePictureUrl = u.ProfilePictureUrl,
                CreatedAt = u.CreatedAt
            };
        }).ToList();

        return new PagedResult<AdminUserSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminUserDetailsDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.OrganizerApplication)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var userRoles = await (from ur in _context.UserRoles
                               where ur.UserId == user.Id
                               join r in _context.Roles on ur.RoleId equals r.Id
                               select r.Name)
                               .ToListAsync(cancellationToken);

        var roles = userRoles.Count > 0 ? (IReadOnlyList<string>)userRoles : new[] { AppRoles.Customer };
        var primaryRole = roles.Contains(AppRoles.Administrator)
            ? AppRoles.Administrator
            : (roles.Contains(AppRoles.Organizer) ? AppRoles.Organizer : AppRoles.Customer);

        AdminOrganizerApplicationSummaryDto? appSummary = null;
        if (user.OrganizerApplication != null)
        {
            appSummary = new AdminOrganizerApplicationSummaryDto
            {
                Id = user.OrganizerApplication.Id,
                Status = user.OrganizerApplication.Status.ToString(),
                OrganizerName = user.OrganizerApplication.OrganizerName,
                OrganizerType = user.OrganizerApplication.OrganizerType.ToString(),
                SubmittedAt = user.OrganizerApplication.SubmittedAt,
                ReviewComment = user.OrganizerApplication.ReviewComment
            };
        }

        return new AdminUserDetailsDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            CountryCode = user.CountryCode,
            Role = primaryRole,
            Roles = roles,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            ProfileCompleted = user.ProfileCompleted,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            OrganizerApplication = appSummary
        };
    }

    public async Task<AdminPlatformOverviewDto> GetPlatformOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
        var inactiveUsers = await _context.Users.CountAsync(u => !u.IsActive, cancellationToken);

        var adminRoleId = await _context.Roles
            .Where(r => r.NormalizedName == AppRoles.Administrator.ToUpper() || (r.Name != null && r.Name.ToUpper() == AppRoles.Administrator.ToUpper()))
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var organizerRoleId = await _context.Roles
            .Where(r => r.NormalizedName == AppRoles.Organizer.ToUpper() || (r.Name != null && r.Name.ToUpper() == AppRoles.Organizer.ToUpper()))
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var customerRoleId = await _context.Roles
            .Where(r => r.NormalizedName == AppRoles.Customer.ToUpper() || (r.Name != null && r.Name.ToUpper() == AppRoles.Customer.ToUpper()))
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var totalAdmins = adminRoleId != Guid.Empty
            ? await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId, cancellationToken)
            : 0;

        var totalOrganizers = organizerRoleId != Guid.Empty
            ? await _context.UserRoles.CountAsync(ur => ur.RoleId == organizerRoleId, cancellationToken)
            : 0;

        var totalCustomers = customerRoleId != Guid.Empty
            ? await _context.UserRoles.CountAsync(ur => ur.RoleId == customerRoleId, cancellationToken)
            : 0;

        var pendingApps = await _context.OrganizerApplications
            .CountAsync(a => a.Status == OrganizerApplicationStatus.Pending, cancellationToken);

        var eventsMetric = await _metricsClient.GetEventsMetricAsync(cancellationToken);
        var bookingsMetric = await _metricsClient.GetBookingsMetricAsync(cancellationToken);

        return new AdminPlatformOverviewDto
        {
            Users = new UserMetricsDto
            {
                OwningService = "EventPulse.IdentityService",
                TotalUsers = totalUsers,
                TotalCustomers = totalCustomers,
                TotalOrganizers = totalOrganizers,
                TotalAdministrators = totalAdmins,
                ActiveUsers = activeUsers,
                InactiveUsers = inactiveUsers,
                PendingOrganizerApplications = pendingApps
            },
            Events = eventsMetric,
            Bookings = bookingsMetric
        };
    }
}
