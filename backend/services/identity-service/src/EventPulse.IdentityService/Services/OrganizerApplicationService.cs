using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Service implementing business rules for organizer applications.
/// </summary>
public class OrganizerApplicationService : IOrganizerApplicationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>>? _roleManager;
    private readonly ILogger<OrganizerApplicationService> _logger;

    public OrganizerApplicationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<OrganizerApplicationService> logger)
        : this(context, userManager, null, logger)
    {
    }

    public OrganizerApplicationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>>? roleManager,
        ILogger<OrganizerApplicationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(OrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> SubmitApplicationAsync(
        Guid userId,
        CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------------
        // 1. Authoritative Domain Validation
        // -------------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(request.OrganizerName))
            return (null, "Organizer name is required.", 400, "VALIDATION_ERROR");

        var trimmedName = request.OrganizerName.Trim();
        if (trimmedName.Length > 200)
            return (null, "Organizer name cannot exceed 200 characters.", 400, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(request.OrganizerType) ||
            !Enum.TryParse<OrganizerType>(request.OrganizerType.Trim(), ignoreCase: true, out var parsedType))
        {
            return (null, "Organizer type must be 'Individual' or 'Organization'.", 400, "VALIDATION_ERROR");
        }

        if (string.IsNullOrWhiteSpace(request.ContactNumber))
            return (null, "Contact number is required.", 400, "VALIDATION_ERROR");

        var trimmedContact = request.ContactNumber.Trim();
        if (trimmedContact.Length < 7 || trimmedContact.Length > 50)
            return (null, "Contact number must be between 7 and 50 characters.", 400, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(request.Description))
            return (null, "Description is required.", 400, "VALIDATION_ERROR");

        var trimmedDescription = request.Description.Trim();
        if (trimmedDescription.Length > 2000)
            return (null, "Description cannot exceed 2000 characters.", 400, "VALIDATION_ERROR");

        string? validatedWebsite = null;
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            var trimmedWebsite = request.Website.Trim();
            if (trimmedWebsite.Length > 500)
                return (null, "Website URL cannot exceed 500 characters.", 400, "VALIDATION_ERROR");

            if (!Uri.TryCreate(trimmedWebsite, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (null, "Website must be a valid absolute HTTP or HTTPS URL.", 400, "VALIDATION_ERROR");
            }
            validatedWebsite = trimmedWebsite;
        }

        // -------------------------------------------------------------------
        // 2. User & Role Verification
        // -------------------------------------------------------------------
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return (null, "User account not found or inactive.", 401, "UNAUTHORIZED");
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(AppRoles.Organizer))
        {
            return (null, "User is already an approved Organizer.", 409, "ALREADY_ORGANIZER");
        }

        if (roles.Contains(AppRoles.Administrator) && !roles.Contains(AppRoles.Customer))
        {
            return (null, "Administrator accounts cannot apply for an organizer profile directly.", 403, "ADMINISTRATOR_ACCOUNT");
        }

        // -------------------------------------------------------------------
        // 3. One Application Per User Verification
        // -------------------------------------------------------------------
        var existingApplication = await _context.OrganizerApplications
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (existingApplication is not null)
        {
            return existingApplication.Status switch
            {
                OrganizerApplicationStatus.Pending =>
                    (null, "An organizer application is already pending review.", 409, "APPLICATION_PENDING"),
                OrganizerApplicationStatus.Approved =>
                    (null, "An organizer application has already been approved.", 409, "APPLICATION_APPROVED"),
                OrganizerApplicationStatus.Rejected =>
                    (null, "A previous application was rejected. Please use the resubmission workflow.", 409, "APPLICATION_REJECTED"),
                _ => (null, "An application record already exists.", 409, "APPLICATION_EXISTS")
            };
        }

        // -------------------------------------------------------------------
        // 4. Server-Controlled Application Creation
        // -------------------------------------------------------------------
        var application = new OrganizerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizerName = trimmedName,
            OrganizerType = parsedType,
            ContactNumber = trimmedContact,
            Description = trimmedDescription,
            Website = validatedWebsite,
            Status = OrganizerApplicationStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
            ReviewedAt = null,
            ReviewedBy = null,
            ReviewComment = null
        };

        _context.OrganizerApplications.Add(application);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on submitting organizer application for UserId={UserId}", userId);
            return (null, "An application has already been created for this account.", 409, "APPLICATION_EXISTS");
        }

        _logger.LogInformation(
            "Organizer application submitted. ApplicationId={ApplicationId} UserId={UserId} Status={Status}",
            application.Id, userId, application.Status);

        var responseDto = MapToDto(application);
        return (responseDto, null, 201, null);
    }

    /// <inheritdoc/>
    public async Task<OrganizerApplicationDto?> GetMyApplicationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var application = await _context.OrganizerApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        return application is null ? null : MapToDto(application);
    }

    /// <inheritdoc/>
    public async Task<(OrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> ResubmitApplicationAsync(
        Guid userId,
        CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Authoritative Domain Validation
        if (string.IsNullOrWhiteSpace(request.OrganizerName))
            return (null, "Organizer name is required.", 400, "VALIDATION_ERROR");

        var trimmedName = request.OrganizerName.Trim();
        if (trimmedName.Length > 200)
            return (null, "Organizer name cannot exceed 200 characters.", 400, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(request.OrganizerType) ||
            !Enum.TryParse<OrganizerType>(request.OrganizerType.Trim(), ignoreCase: true, out var parsedType))
        {
            return (null, "Organizer type must be 'Individual' or 'Organization'.", 400, "VALIDATION_ERROR");
        }

        if (string.IsNullOrWhiteSpace(request.ContactNumber))
            return (null, "Contact number is required.", 400, "VALIDATION_ERROR");

        var trimmedContact = request.ContactNumber.Trim();
        if (trimmedContact.Length < 7 || trimmedContact.Length > 50)
            return (null, "Contact number must be between 7 and 50 characters.", 400, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(request.Description))
            return (null, "Description is required.", 400, "VALIDATION_ERROR");

        var trimmedDescription = request.Description.Trim();
        if (trimmedDescription.Length > 2000)
            return (null, "Description cannot exceed 2000 characters.", 400, "VALIDATION_ERROR");

        string? validatedWebsite = null;
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            var trimmedWebsite = request.Website.Trim();
            if (trimmedWebsite.Length > 500)
                return (null, "Website URL cannot exceed 500 characters.", 400, "VALIDATION_ERROR");

            if (!Uri.TryCreate(trimmedWebsite, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (null, "Website must be a valid absolute HTTP or HTTPS URL.", 400, "VALIDATION_ERROR");
            }
            validatedWebsite = trimmedWebsite;
        }

        // 2. User & Application Lookup
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return (null, "User account not found or inactive.", 401, "UNAUTHORIZED");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Organizer))
        {
            return (null, "User is already an approved Organizer.", 409, "ALREADY_ORGANIZER");
        }

        var application = await _context.OrganizerApplications
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (application is null)
        {
            return (null, "No organizer application found to resubmit.", 404, "APPLICATION_NOT_FOUND");
        }

        if (application.Status != OrganizerApplicationStatus.Rejected)
        {
            return (null, $"Only rejected applications can be resubmitted. Current status is {application.Status}.", 409, "INVALID_STATE_TRANSITION");
        }

        // 3. Update the existing record (Rejected -> Pending)
        application.OrganizerName = trimmedName;
        application.OrganizerType = parsedType;
        application.ContactNumber = trimmedContact;
        application.Description = trimmedDescription;
        application.Website = validatedWebsite;
        application.Status = OrganizerApplicationStatus.Pending;
        application.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Organizer application resubmitted. ApplicationId={ApplicationId} UserId={UserId} Status=Pending",
            application.Id, userId);

        return (MapToDto(application), null, 200, null);
    }

    /// <inheritdoc/>
    public async Task<List<AdminOrganizerApplicationDto>> GetAdminApplicationsAsync(
        OrganizerApplicationStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OrganizerApplications
            .AsNoTracking()
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(a => a.Status == statusFilter.Value);
        }

        var list = await query
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(cancellationToken);

        var userIds = list.Select(a => a.UserId).Distinct().ToList();
        var emailMap = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? string.Empty, cancellationToken);

        return list.Select(a => MapToAdminDto(a, emailMap.GetValueOrDefault(a.UserId, string.Empty))).ToList();
    }

    /// <inheritdoc/>
    public async Task<AdminOrganizerApplicationDto?> GetAdminApplicationByIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var application = await _context.OrganizerApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null) return null;

        var user = await _userManager.FindByIdAsync(application.UserId.ToString());
        return MapToAdminDto(application, user?.Email ?? string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(AdminOrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> ApproveApplicationAsync(
        Guid applicationId,
        Guid adminId,
        ApproveOrganizerApplicationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var application = await _context.OrganizerApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return (null, "Organizer application not found.", 404, "APPLICATION_NOT_FOUND");
        }

        if (application.Status != OrganizerApplicationStatus.Pending)
        {
            return (null, $"Only pending applications can be approved. Current status is {application.Status}.", 409, "INVALID_STATE_TRANSITION");
        }

        var user = await _userManager.FindByIdAsync(application.UserId.ToString());
        if (user is null)
        {
            return (null, "Applicant user account not found.", 404, "USER_NOT_FOUND");
        }

        // Atomic EF Core transaction spanning Application status update and Role grant
        var isRelational = _context.Database.IsRelational();
        await using var tx = isRelational ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            application.Status = OrganizerApplicationStatus.Approved;
            application.ReviewedAt = DateTime.UtcNow;
            application.ReviewedBy = adminId;
            if (!string.IsNullOrWhiteSpace(request?.ReviewComment))
            {
                application.ReviewComment = request.ReviewComment.Trim();
            }

            // Ensure Organizer role exists
            if (_roleManager is not null && !await _roleManager.RoleExistsAsync(AppRoles.Organizer))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(AppRoles.Organizer));
            }

            // Grant Organizer role (preserving Customer role)
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(AppRoles.Organizer))
            {
                var addRoleResult = await _userManager.AddToRoleAsync(user, AppRoles.Organizer);
                if (!addRoleResult.Succeeded)
                {
                    if (tx is not null) await tx.RollbackAsync(cancellationToken);
                    var errDesc = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign Organizer role to user {UserId}: {Error}", user.Id, errDesc);
                    return (null, $"Failed to grant Organizer role: {errDesc}", 500, "ROLE_ASSIGNMENT_FAILED");
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed while approving organizer application {ApplicationId}", applicationId);
            return (null, "Failed to approve application due to an internal error.", 500, "TRANSACTION_ERROR");
        }

        _logger.LogInformation(
            "Organizer application approved. ApplicationId={ApplicationId} ApplicantUserId={ApplicantUserId} ReviewerAdminId={AdminId}",
            application.Id, user.Id, adminId);

        return (MapToAdminDto(application, user.Email ?? string.Empty), null, 200, null);
    }

    /// <inheritdoc/>
    public async Task<(AdminOrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> RejectApplicationAsync(
        Guid applicationId,
        Guid adminId,
        RejectOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.ReviewComment))
        {
            return (null, "Rejection feedback comment is required.", 400, "VALIDATION_ERROR");
        }

        var trimmedComment = request.ReviewComment.Trim();
        if (trimmedComment.Length > 1000)
        {
            return (null, "Rejection comment cannot exceed 1000 characters.", 400, "VALIDATION_ERROR");
        }

        var application = await _context.OrganizerApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return (null, "Organizer application not found.", 404, "APPLICATION_NOT_FOUND");
        }

        if (application.Status != OrganizerApplicationStatus.Pending)
        {
            return (null, $"Only pending applications can be rejected. Current status is {application.Status}.", 409, "INVALID_STATE_TRANSITION");
        }

        application.Status = OrganizerApplicationStatus.Rejected;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedBy = adminId;
        application.ReviewComment = trimmedComment;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Organizer application rejected. ApplicationId={ApplicationId} ApplicantUserId={ApplicantUserId} ReviewerAdminId={AdminId}",
            application.Id, application.UserId, adminId);

        var user = await _userManager.FindByIdAsync(application.UserId.ToString());
        var userEmail = user?.Email ?? string.Empty;
        return (MapToAdminDto(application, userEmail), null, 200, null);
    }

    private static OrganizerApplicationDto MapToDto(OrganizerApplication app) => new()
    {
        Id = app.Id,
        OrganizerName = app.OrganizerName,
        OrganizerType = app.OrganizerType.ToString(),
        ContactNumber = app.ContactNumber,
        Description = app.Description,
        Website = app.Website,
        Status = app.Status.ToString(),
        SubmittedAt = app.SubmittedAt,
        ReviewedAt = app.ReviewedAt,
        ReviewComment = app.ReviewComment
    };

    private static AdminOrganizerApplicationDto MapToAdminDto(OrganizerApplication app, string accountEmail) => new()
    {
        Id = app.Id,
        UserId = app.UserId,
        AccountEmail = accountEmail,
        OrganizerName = app.OrganizerName,
        OrganizerType = app.OrganizerType.ToString(),
        ContactNumber = app.ContactNumber,
        Description = app.Description,
        Website = app.Website,
        Status = app.Status.ToString(),
        SubmittedAt = app.SubmittedAt,
        ReviewedAt = app.ReviewedAt,
        ReviewedBy = app.ReviewedBy,
        ReviewComment = app.ReviewComment
    };
}
