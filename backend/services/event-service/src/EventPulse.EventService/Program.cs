using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventPulse.EventService;
using EventPulse.EventService.Configuration;
using EventPulse.EventService.Data;
using Prometheus;
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
// JWT Bearer — validates tokens issued by Identity Service
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKeyStr = jwtSection["Key"] ?? "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";
var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyStr);

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
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes) { KeyId = "EventPulseKey_2026" },
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
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("EventService.JwtAuthentication");
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("EventService.JwtAuthentication");
            logger.LogWarning("JWT challenge triggered: Error={Error}, Description={Description}",
                context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
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
builder.Services.AddScoped<IEventUpdateRequestService, EventUpdateRequestService>();
builder.Services.AddScoped<IEventCancellationRequestService, EventCancellationRequestService>();

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

// Application Insights Telemetry (EP-200 / TECH-11)
var appInsightsConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                   ?? builder.Configuration["ApplicationInsights:ConnectionString"];

if (!string.IsNullOrWhiteSpace(appInsightsConn))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConn;
    });
}

var app = builder.Build();

app.UseRouting();

// Prometheus HTTP Request Metrics (TECH-12)
app.UseHttpMetrics();

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

if (app.Environment.IsDevelopment() || dbSettings.MigrateOnStartup || dbSettings.SeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

    if (app.Environment.IsDevelopment() || dbSettings.MigrateOnStartup)
    {
        app.Logger.LogInformation("Executing EF Core database migrations...");
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }

    if (app.Environment.IsDevelopment() || dbSettings.SeedOnStartup)
    {
        app.Logger.LogInformation("Executing database seeding...");
        await EventDbSeeder.SeedAsync(dbContext);
        app.Logger.LogInformation("Database seeding completed successfully.");
    }
}

app.UseCors("AllowAll");

// Authentication must precede Authorization in the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();

// Health endpoint
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint (TECH-12)
app.MapMetrics();

app.MapControllers();

await app.RunAsync();