using System.Security.Claims;
using System.Text;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Booking Service
// ---------------------------------------------------------------------------
// Owns: ticket reservations, seat availability, booking lifecycle.
// Does NOT reference: IdentityService, EventService, PaymentService.
// Communicates with EventService (read event/seat data) → via HTTP client.
// ---------------------------------------------------------------------------

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BookingDatabase")));

builder.Services.AddHttpClient<IEventAvailabilityClient, EventServiceAvailabilityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventService:BaseUrl"] ?? "http://localhost:7102");
});

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
var keyStr = builder.Configuration["Jwt:Key"] 
    ?? builder.Configuration["Jwt__Key"] 
    ?? jwtSection["Key"]
    ?? "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";

using (var sha256 = System.Security.Cryptography.SHA256.Create())
{
    var hash = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(keyStr)));
    Console.WriteLine($"[KEY-VERIFY] {builder.Environment.ApplicationName} Key Hash: {hash}");
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(keyStr);

var keyedSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
{
    KeyId = "EventPulseKey_2026"
};
var unkeyedSigningKey = new SymmetricSecurityKey(jwtKeyBytes);

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
        IssuerSigningKey = keyedSigningKey,
        IssuerSigningKeys = new SecurityKey[] { keyedSigningKey, unkeyedSigningKey },
        IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            return new SecurityKey[] { keyedSigningKey, unkeyedSigningKey };
        },
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? builder.Configuration["Jwt:Issuer"] ?? "EventPulse.IdentityService",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? builder.Configuration["Jwt:Audience"] ?? "EventPulse.Clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("BookingService.JwtAuthentication");
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("BookingService.JwtAuthentication");
            logger.LogWarning("JWT challenge triggered: Error={Error}, Description={Description}",
                context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IBookingReferenceGenerator, BookingReferenceGenerator>();
builder.Services.AddSingleton<IBookingEventPublisher, LoggingBookingEventPublisher>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database Migration on Startup
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

// Prometheus HTTP metrics middleware
app.UseHttpMetrics();

// Middleware Ordering: CORS -> Authentication -> Authorization -> Endpoints
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint
app.MapMetrics();

app.MapControllers();

app.Run();