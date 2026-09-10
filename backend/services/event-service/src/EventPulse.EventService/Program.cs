using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventPulse.EventService;
using EventPulse.EventService.Configuration;
using EventPulse.EventService.Data;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Event Service
// ---------------------------------------------------------------------------
// Owns: events, categories, venues, schedules.
// Does NOT reference: IdentityService, BookingService, PaymentService.
// Authorization is based entirely on the signed EventPulse JWT — the Event
// Service never queries the Identity database.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<EventDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EventDatabase")));

// ---------------------------------------------------------------------------
// JWT Bearer — validates tokens issued by Identity Service
// ---------------------------------------------------------------------------
// The signing key MUST match the key in IdentityService.
// Supply via:
//   Local dev: dotnet user-secrets set "Jwt:Key" "<same-key>"
//   Azure:     App Service environment variable Jwt__Key
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKeyStr = jwtSection["Key"];
var jwtKeyBytes = !string.IsNullOrEmpty(jwtKeyStr)
    ? Encoding.UTF8.GetBytes(jwtKeyStr)
    : new byte[32]; // fallback — token validation will fail at runtime without a real key

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? "EventPulse.IdentityService",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? "EventPulse.Clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        // Map role claims correctly so [Authorize(Policy = ...)] works
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier,
    };
});

// ---------------------------------------------------------------------------
// Authorization Policies
// ---------------------------------------------------------------------------
// These mirror the policies in Identity Service so future business endpoints
// can use [Authorize(Policy = AppPolicies.OrganizerOnly)] consistently.
// No endpoints are protected in Phase 1 — public event browsing remains open.
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.OrganizerOnly, policy =>
        policy.RequireRole("Organizer"));

    options.AddPolicy(AppPolicies.AdministratorOnly, policy =>
        policy.RequireRole("Administrator"));
});

// ---------------------------------------------------------------------------
// Application Services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IEventSubmissionService, EventSubmissionService>();
builder.Services.AddScoped<IEventReviewService, EventReviewService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();

// ---------------------------------------------------------------------------
// Infrastructure — Blob Storage
// ---------------------------------------------------------------------------
// Local dev: Azurite connection string in appsettings.Development.json
// Production: BlobStorage__ConnectionString environment variable
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IEventImageStorage, AzureBlobEventImageStorage>();

// ---------------------------------------------------------------------------
// API & Infrastructure
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ---------------------------------------------------------------------------
// Database Migration & Seeding on Startup
// ---------------------------------------------------------------------------
var dbSettings = app.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>() ?? new DatabaseSettings();

if (dbSettings.MigrateOnStartup || dbSettings.SeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

    if (dbSettings.MigrateOnStartup)
    {
        app.Logger.LogInformation("Executing EF Core database migrations (Database:MigrateOnStartup = true)...");
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }

    if (dbSettings.SeedOnStartup)
    {
        app.Logger.LogInformation("Executing database seeding (Database:SeedOnStartup = true)...");
        await EventDbSeeder.SeedAsync(dbContext);
        app.Logger.LogInformation("Database seeding completed successfully.");
    }
}

// Authentication must precede Authorization in the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
