using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private static readonly SemaphoreSlim _adminMutationLock = new(1, 1);

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser>? _userManager;
    private readonly IPlatformMetricsClient _metricsClient;
    private readonly ILogger<AdminManagementService> _logger;

    public AdminManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPlatformMetricsClient metricsClient,
        ILogger<AdminManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _metricsClient = metricsClient;
        _logger = logger;
    }

    public AdminManagementService(
        ApplicationDbContext context,
        IPlatformMetricsClient metricsClient,
        ILogger<AdminManagementService> logger)
        : this(context, null!, metricsClient, logger)
    {
    }

    private async Task<bool> IsUserInRoleInternalAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        if (_userManager != null)
        {
            try { return await _userManager.IsInRoleAsync(user, roleName); } catch { }
        }

        var normalized = roleName.ToUpperInvariant();
        return await (from ur in _context.UserRoles
                      join r in _context.Roles on ur.RoleId equals r.Id
                      where ur.UserId == user.Id && (r.NormalizedName == normalized || (r.Name != null && r.Name.ToUpper() == normalized))
                      select ur.UserId).AnyAsync(ct);
    }

    private async Task<IList<string>> GetUserRolesInternalAsync(ApplicationUser user, CancellationToken ct)
    {
        if (_userManager != null)
        {
            try { return await _userManager.GetRolesAsync(user); } catch { }
        }

        return await (from ur in _context.UserRoles
                      join r in _context.Roles on ur.RoleId equals r.Id
                      where ur.UserId == user.Id
                      select r.Name!).ToListAsync(ct);
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

    public async Task<AdminActionResult> ActivateUserAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (targetUser == null)
        {
            return AdminActionResult.Failure(404, "USER_NOT_FOUND", $"User with ID '{targetUserId}' was not found.");
        }

        if (targetUser.IsActive)
        {
            return AdminActionResult.Failure(400, "INVALID_STATE_TRANSITION", "User account is already active.");
        }

        var actorAdmin = _userManager != null
            ? await _userManager.FindByIdAsync(actorAdminId.ToString())
            : await _context.Users.FirstOrDefaultAsync(u => u.Id == actorAdminId, cancellationToken);
        var actorEmail = actorAdmin?.Email ?? "admin@eventpulse.lk";

        await _adminMutationLock.WaitAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            var prevState = JsonSerializer.Serialize(new { isActive = false });

            targetUser.IsActive = true;
            targetUser.LockoutEnd = null;
            targetUser.AccessFailedCount = 0;

            if (_userManager != null)
            {
                await _userManager.UpdateSecurityStampAsync(targetUser);
                await _userManager.UpdateAsync(targetUser);
            }
            else
            {
                targetUser.SecurityStamp = Guid.NewGuid().ToString();
                _context.Users.Update(targetUser);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var newState = JsonSerializer.Serialize(new { isActive = true });

            var audit = new AdminAuditLog
            {
                ActorAdminId = actorAdminId,
                ActorEmail = actorEmail,
                TargetUserId = targetUserId,
                TargetEmail = targetUser.Email ?? string.Empty,
                Action = "ADMIN_USER_ACTIVATED",
                PreviousState = prevState,
                NewState = newState,
                Reason = reason,
                Outcome = "SUCCESS",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };
            _context.AdminAuditLogs.Add(audit);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("AUDIT: Admin {AdminId} ({AdminEmail}) activated user {TargetUserId} ({TargetEmail}). Reason: {Reason}",
                actorAdminId, actorEmail, targetUserId, targetUser.Email, reason ?? "N/A");

            var userDetails = await GetUserByIdAsync(targetUserId, cancellationToken);
            return AdminActionResult.Success("User account activated successfully.", userDetails);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "Failed to activate user {TargetUserId}", targetUserId);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
            _adminMutationLock.Release();
        }
    }

    public async Task<AdminActionResult> DeactivateUserAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (actorAdminId == targetUserId)
        {
            return AdminActionResult.Failure(400, "SELF_DEACTIVATION_PROHIBITED", "Administrators cannot deactivate their own account.");
        }

        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (targetUser == null)
        {
            return AdminActionResult.Failure(404, "USER_NOT_FOUND", $"User with ID '{targetUserId}' was not found.");
        }

        if (!targetUser.IsActive)
        {
            return AdminActionResult.Failure(400, "INVALID_STATE_TRANSITION", "User account is already deactivated.");
        }

        var actorAdmin = _userManager != null
            ? await _userManager.FindByIdAsync(actorAdminId.ToString())
            : await _context.Users.FirstOrDefaultAsync(u => u.Id == actorAdminId, cancellationToken);
        var actorEmail = actorAdmin?.Email ?? "admin@eventpulse.lk";

        await _adminMutationLock.WaitAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            var isTargetAdmin = await IsUserInRoleInternalAsync(targetUser, AppRoles.Administrator, cancellationToken);
            if (isTargetAdmin)
            {
                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == AppRoles.Administrator.ToUpper(), cancellationToken);
                var activeAdminCount = adminRole != null
                    ? await (from ur in _context.UserRoles
                             join u in _context.Users on ur.UserId equals u.Id
                             where ur.RoleId == adminRole.Id && u.IsActive
                             select ur.UserId).CountAsync(cancellationToken)
                    : 0;

                if (activeAdminCount <= 1)
                {
                    return AdminActionResult.Failure(400, "LAST_ADMIN_PROTECTION", "Cannot deactivate the last remaining active Administrator on the platform.");
                }
            }

            var prevState = JsonSerializer.Serialize(new { isActive = true });

            targetUser.IsActive = false;

            if (_userManager != null)
            {
                await _userManager.UpdateSecurityStampAsync(targetUser);
                await _userManager.UpdateAsync(targetUser);
            }
            else
            {
                targetUser.SecurityStamp = Guid.NewGuid().ToString();
                _context.Users.Update(targetUser);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var newState = JsonSerializer.Serialize(new { isActive = false });

            var audit = new AdminAuditLog
            {
                ActorAdminId = actorAdminId,
                ActorEmail = actorEmail,
                TargetUserId = targetUserId,
                TargetEmail = targetUser.Email ?? string.Empty,
                Action = "ADMIN_USER_DEACTIVATED",
                PreviousState = prevState,
                NewState = newState,
                Reason = reason,
                Outcome = "SUCCESS",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };
            _context.AdminAuditLogs.Add(audit);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("AUDIT: Admin {AdminId} ({AdminEmail}) deactivated user {TargetUserId} ({TargetEmail}). Reason: {Reason}",
                actorAdminId, actorEmail, targetUserId, targetUser.Email, reason ?? "N/A");

            var userDetails = await GetUserByIdAsync(targetUserId, cancellationToken);
            return AdminActionResult.Success("User account deactivated successfully.", userDetails);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "Failed to deactivate user {TargetUserId}", targetUserId);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
            _adminMutationLock.Release();
        }
    }

    public async Task<AdminActionResult> ChangeUserRoleAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string newRole,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newRole))
        {
            return AdminActionResult.Failure(400, "INVALID_ROLE_SPECIFIED", "Role name cannot be empty.");
        }

        var normalizedNewRole = newRole.Trim();
        var canonicalRole = normalizedNewRole.Equals(AppRoles.Customer, StringComparison.OrdinalIgnoreCase) ? AppRoles.Customer
            : normalizedNewRole.Equals(AppRoles.Organizer, StringComparison.OrdinalIgnoreCase) ? AppRoles.Organizer
            : normalizedNewRole.Equals(AppRoles.Administrator, StringComparison.OrdinalIgnoreCase) ? AppRoles.Administrator
            : null;

        if (canonicalRole == null)
        {
            return AdminActionResult.Failure(400, "INVALID_ROLE_SPECIFIED", "The specified role is invalid. Allowed roles: Customer, Organizer, Administrator.");
        }

        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (targetUser == null)
        {
            return AdminActionResult.Failure(404, "USER_NOT_FOUND", $"User with ID '{targetUserId}' was not found.");
        }

        var actorAdmin = _userManager != null
            ? await _userManager.FindByIdAsync(actorAdminId.ToString())
            : await _context.Users.FirstOrDefaultAsync(u => u.Id == actorAdminId, cancellationToken);
        var actorEmail = actorAdmin?.Email ?? "admin@eventpulse.lk";

        var currentRoles = await GetUserRolesInternalAsync(targetUser, cancellationToken);
        var isCurrentlyAdmin = currentRoles.Contains(AppRoles.Administrator);

        if (actorAdminId == targetUserId && isCurrentlyAdmin && !canonicalRole.Equals(AppRoles.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            return AdminActionResult.Failure(400, "SELF_DEMOTION_PROHIBITED", "Administrators cannot remove administrative privileges from their own account. Another administrator must perform this action.");
        }

        if (currentRoles.Count == 1 && currentRoles[0].Equals(canonicalRole, StringComparison.OrdinalIgnoreCase))
        {
            return AdminActionResult.Failure(400, "INVALID_STATE_TRANSITION", $"User already holds the {canonicalRole} role.");
        }

        await _adminMutationLock.WaitAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            if (isCurrentlyAdmin && targetUser.IsActive && !canonicalRole.Equals(AppRoles.Administrator, StringComparison.OrdinalIgnoreCase))
            {
                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == AppRoles.Administrator.ToUpper(), cancellationToken);
                var activeAdminCount = adminRole != null
                    ? await (from ur in _context.UserRoles
                             join u in _context.Users on ur.UserId equals u.Id
                             where ur.RoleId == adminRole.Id && u.IsActive
                             select ur.UserId).CountAsync(cancellationToken)
                    : 0;

                if (activeAdminCount <= 1)
                {
                    return AdminActionResult.Failure(400, "LAST_ADMIN_PROTECTION", "Cannot demote the last remaining active Administrator on the platform.");
                }
            }

            var prevState = JsonSerializer.Serialize(new { roles = currentRoles });

            if (currentRoles.Count > 0)
            {
                if (_userManager != null)
                {
                    await _userManager.RemoveFromRolesAsync(targetUser, currentRoles);
                }
                else
                {
                    var existingUserRoles = await _context.UserRoles.Where(ur => ur.UserId == targetUser.Id).ToListAsync(cancellationToken);
                    _context.UserRoles.RemoveRange(existingUserRoles);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == canonicalRole.ToUpper(), cancellationToken);
            if (roleEntity == null)
            {
                roleEntity = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = canonicalRole,
                    NormalizedName = canonicalRole.ToUpper()
                };
                _context.Roles.Add(roleEntity);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (_userManager != null)
            {
                await _userManager.AddToRoleAsync(targetUser, canonicalRole);
            }
            else
            {
                _context.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = targetUser.Id,
                    RoleId = roleEntity.Id
                });
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (_userManager != null)
            {
                await _userManager.UpdateSecurityStampAsync(targetUser);
                await _userManager.UpdateAsync(targetUser);
            }
            else
            {
                targetUser.SecurityStamp = Guid.NewGuid().ToString();
                _context.Users.Update(targetUser);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var newState = JsonSerializer.Serialize(new { roles = new[] { canonicalRole } });

            var audit = new AdminAuditLog
            {
                ActorAdminId = actorAdminId,
                ActorEmail = actorEmail,
                TargetUserId = targetUserId,
                TargetEmail = targetUser.Email ?? string.Empty,
                Action = "ADMIN_USER_ROLE_CHANGED",
                PreviousState = prevState,
                NewState = newState,
                Reason = reason,
                Outcome = "SUCCESS",
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };
            _context.AdminAuditLogs.Add(audit);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("AUDIT: Admin {AdminId} ({AdminEmail}) changed role for user {TargetUserId} ({TargetEmail}) from {OldRoles} to {NewRole}. Reason: {Reason}",
                actorAdminId, actorEmail, targetUserId, targetUser.Email, string.Join(",", currentRoles), canonicalRole, reason ?? "N/A");

            var userDetails = await GetUserByIdAsync(targetUserId, cancellationToken);
            return AdminActionResult.Success($"User role changed to {canonicalRole} successfully.", userDetails);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogError(ex, "Failed to change role for user {TargetUserId}", targetUserId);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
            _adminMutationLock.Release();
        }
    }
}
