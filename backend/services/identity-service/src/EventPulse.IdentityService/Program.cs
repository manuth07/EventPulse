using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;
using Prometheus;

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

        // Lockout
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// Application & Security Services
// ---------------------------------------------------------------------------
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
var keyStr = builder.Configuration["Jwt:Key"] 
    ?? builder.Configuration["Jwt__Key"] 
    ?? builder.Configuration.GetSection("Jwt")["Key"]
    ?? "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";

using (var sha256 = System.Security.Cryptography.SHA256.Create())
{
    var hash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(keyStr)));
    Console.WriteLine($"[KEY-VERIFY] {builder.Environment.ApplicationName} Key Hash: {hash}");
}

builder.Services.Configure<JwtSettings>(options =>
{
    builder.Configuration.GetSection("Jwt").Bind(options);
    if (string.IsNullOrWhiteSpace(options.Key))
    {
        options.Key = keyStr;
    }
});
builder.Services.Configure<GoogleSettings>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<AdminBootstrapSettings>(builder.Configuration.GetSection("AdminBootstrap"));
builder.Services.Configure<DevOrganizerSettings>(builder.Configuration.GetSection("DevOrganizer"));

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ISetPasswordService, SetPasswordService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IOrganizerApplicationService, OrganizerApplicationService>();
builder.Services.AddScoped<IdentityDataSeeder>();

// ---------------------------------------------------------------------------
// CORS Policy
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// JWT Bearer Authentication Infrastructure
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var keyBytes = Encoding.UTF8.GetBytes(keyStr);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes) { KeyId = "EventPulseKey_2026" },
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? "EventPulse.IdentityService",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? "EventPulse.Clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("IdentityService.JwtAuthentication");
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("IdentityService.JwtAuthentication");
            logger.LogWarning("JWT challenge triggered: Error={Error}, Description={Description}",
                context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

// ---------------------------------------------------------------------------
// API, Observability & Infrastructure
// Authorization Policies
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    // Organizer-only endpoints (e.g., create/manage events)
    options.AddPolicy("OrganizerOnly", policy =>
        policy.RequireRole(EventPulse.IdentityService.Models.AppRoles.Organizer));

    // Administrator-only endpoints (e.g., event approval, user management)
    options.AddPolicy("AdministratorOnly", policy =>
        policy.RequireRole(EventPulse.IdentityService.Models.AppRoles.Administrator));
});

// ---------------------------------------------------------------------------
// API & Infrastructure
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Application Insights Telemetry (EP-200 / TECH-11)
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Prometheus HTTP Request Metrics (TECH-12)
app.UseRouting();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Health endpoint consumed by YARP gateway health-check and load balancer
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint (TECH-12)
app.MapMetrics();

app.MapControllers();

// ---------------------------------------------------------------------------
// Startup: Migrate database, seed roles and optional bootstrap admin
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();
