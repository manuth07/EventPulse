using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventPulse.EventService;
using EventPulse.EventService.Data;

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
// API & Infrastructure
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();
        await EventDbSeeder.SeedAsync(dbContext);
    }
}

// Authentication must precede Authorization in the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

