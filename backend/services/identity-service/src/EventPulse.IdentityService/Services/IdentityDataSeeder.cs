using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Runs once on application startup to ensure the EventPulse role model is in place
/// and to optionally provision bootstrap accounts.
///
/// This seeder is IDEMPOTENT:
/// - Roles that already exist are not duplicated.
/// - Accounts that already exist are not modified, recreated, or have their password reset.
/// - It is safe to restart the service with bootstrap enabled after provisioning.
///
/// Security contract:
/// - Never uses raw SQL against Identity tables.
/// - Never hard-codes credentials — reads from settings (User Secrets / env).
/// - Does not accept credentials from API requests or frontend payloads.
/// </summary>
public class IdentityDataSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AdminBootstrapSettings _bootstrap;
    private readonly DevOrganizerSettings _devOrganizer;
    private readonly ILogger<IdentityDataSeeder> _logger;

    public IdentityDataSeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<AdminBootstrapSettings> bootstrapOptions,
        IOptions<DevOrganizerSettings> devOrganizerOptions,
        ILogger<IdentityDataSeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _bootstrap = bootstrapOptions.Value;
        _devOrganizer = devOrganizerOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Entry point called from Program.cs startup.
    /// </summary>
    public async Task SeedAsync()
    {
        await EnsureRolesAsync();
        await EnsureBootstrapAdminAsync();
        await EnsureDevOrganizerAsync();
    }

    // -----------------------------------------------------------------------
    // Phase 1: Ensure all system roles exist
    // -----------------------------------------------------------------------

    private async Task EnsureRolesAsync()
    {
        string[] roles = [AppRoles.Customer, AppRoles.Organizer, AppRoles.Administrator];

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (result.Succeeded)
                    _logger.LogInformation("IdentityDataSeeder: Role '{Role}' created.", role);
                else
                    _logger.LogError("IdentityDataSeeder: Failed to create role '{Role}': {Errors}",
                        role, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            else
            {
                _logger.LogDebug("IdentityDataSeeder: Role '{Role}' already exists.", role);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Phase 2: Optionally provision bootstrap Administrator
    // -----------------------------------------------------------------------

    private async Task EnsureBootstrapAdminAsync()
    {
        if (!_bootstrap.Enabled)
        {
            _logger.LogDebug("IdentityDataSeeder: AdminBootstrap is disabled. Skipping admin provisioning.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_bootstrap.Email) || string.IsNullOrWhiteSpace(_bootstrap.Password))
        {
            _logger.LogWarning(
                "IdentityDataSeeder: AdminBootstrap is enabled but Email or Password is not configured. " +
                "Set 'AdminBootstrap:Email' and 'AdminBootstrap:Password' via User Secrets or environment variables.");
            return;
        }

        var email = _bootstrap.Email.Trim().ToLowerInvariant();

        // Idempotency: if admin already exists, do nothing destructive
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            _logger.LogInformation(
                "IdentityDataSeeder: Bootstrap admin '{Email}' already exists (Id={Id}). No changes made.",
                email, existing.Id);
            return;
        }

        // Create the admin user through UserManager (identity handles password hashing)
        var admin = new ApplicationUser
        {
            FirstName = string.IsNullOrWhiteSpace(_bootstrap.FirstName) ? "Admin" : _bootstrap.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(_bootstrap.LastName) ? "EventPulse" : _bootstrap.LastName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,   // Trusted provisioning — no OTP flow
            ProfileCompleted = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(admin, _bootstrap.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogError(
                "IdentityDataSeeder: Failed to create bootstrap admin '{Email}': {Errors}",
                email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        // Assign Administrator role (only Administrator — not Customer or Organizer)
        var roleResult = await _userManager.AddToRoleAsync(admin, AppRoles.Administrator);
        if (!roleResult.Succeeded)
        {
            _logger.LogError(
                "IdentityDataSeeder: Bootstrap admin '{Email}' was created but role assignment failed: {Errors}",
                email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            // Note: user exists but has no role — operator must fix manually or re-provision
            return;
        }

        _logger.LogInformation(
            "IdentityDataSeeder: Bootstrap Administrator provisioned successfully. " +
            "UserId={UserId}, Email={Email}. " +
            "Consider setting 'AdminBootstrap:Enabled=false' and removing the password " +
            "from Azure configuration once provisioning is confirmed.",
            admin.Id, email);
    }

    // -----------------------------------------------------------------------
    // Phase 2: Optionally provision a development Organizer account
    // -----------------------------------------------------------------------

    private async Task EnsureDevOrganizerAsync()
    {
        if (!_devOrganizer.Enabled)
        {
            _logger.LogDebug("IdentityDataSeeder: DevOrganizer is disabled. Skipping.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_devOrganizer.Email) || string.IsNullOrWhiteSpace(_devOrganizer.Password))
        {
            _logger.LogWarning(
                "IdentityDataSeeder: DevOrganizer is enabled but Email or Password is not configured. " +
                "Set 'DevOrganizer:Email' and 'DevOrganizer:Password' via User Secrets.");
            return;
        }

        var email = _devOrganizer.Email.Trim().ToLowerInvariant();

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            _logger.LogInformation(
                "IdentityDataSeeder: DevOrganizer '{Email}' already exists (Id={Id}). No changes made.",
                email, existing.Id);
            return;
        }

        var organizer = new ApplicationUser
        {
            FirstName = string.IsNullOrWhiteSpace(_devOrganizer.FirstName) ? "Organizer" : _devOrganizer.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(_devOrganizer.LastName) ? "Dev" : _devOrganizer.LastName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,    // Trusted development provisioning
            ProfileCompleted = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(organizer, _devOrganizer.Password);
        if (!createResult.Succeeded)
        {
            _logger.LogError(
                "IdentityDataSeeder: Failed to create DevOrganizer '{Email}': {Errors}",
                email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        // Assign ONLY the Organizer role — never Customer or Administrator
        var roleResult = await _userManager.AddToRoleAsync(organizer, AppRoles.Organizer);
        if (!roleResult.Succeeded)
        {
            _logger.LogError(
                "IdentityDataSeeder: DevOrganizer '{Email}' created but role assignment failed: {Errors}",
                email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation(
            "IdentityDataSeeder: Development Organizer provisioned. UserId={UserId}, Email={Email}",
            organizer.Id, email);
    }
}
