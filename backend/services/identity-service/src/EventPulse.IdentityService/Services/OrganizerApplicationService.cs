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
    private readonly ILogger<OrganizerApplicationService> _logger;

    public OrganizerApplicationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<OrganizerApplicationService> logger)
    {
        _context = context;
        _userManager = userManager;
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
}
