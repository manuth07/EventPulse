using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDatabase")));

// ---------------------------------------------------------------------------
// Data Protection (required by Identity token providers)
// ---------------------------------------------------------------------------
builder.Services.AddDataProtection();

// ---------------------------------------------------------------------------
// ASP.NET Core Identity
// ---------------------------------------------------------------------------
// - ApplicationUser: EventPulse account entity (email/password + Google later)
// - IdentityRole<Guid>: Customer, Organizer, Administrator
// - EF Core stores: AspNetUsers, AspNetRoles, AspNetUserRoles,
//                   AspNetUserClaims, AspNetRoleClaims,
//                   AspNetUserLogins (Google external logins),
//                   AspNetUserTokens
// - AddDefaultTokenProviders: enables email-verification token generation later
// ---------------------------------------------------------------------------
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Password policy
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Email is the EventPulse login identifier
        options.User.RequireUniqueEmail = true;

        // Lockout will be configured when login is implemented
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// Application Services
// ---------------------------------------------------------------------------
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// ---------------------------------------------------------------------------
// API & Infrastructure
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

// Health endpoint consumed by YARP gateway health-check and load balancer
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
