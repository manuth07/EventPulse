using System.Text;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Booking Service
// ---------------------------------------------------------------------------
// Owns: ticket reservations, seat availability, booking lifecycle.
// Does NOT reference: IdentityService, EventService, PaymentService.
// Communicates with EventService (read event/seat data) → via HTTP client later.
// Communicates with PaymentService (payment confirmation) → via Kafka later.
// ---------------------------------------------------------------------------

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BookingDatabase")));

builder.Services.AddHttpClient<IEventAvailabilityClient, EventServiceAvailabilityClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventService:BaseUrl"] ?? "http://localhost:7102");
});

// ---------------------------------------------------------------------------
// JWT Bearer — validates tokens issued by Identity Service
// ---------------------------------------------------------------------------
// The signing key MUST match the key configured in IdentityService.
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
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IBookingReferenceGenerator, BookingReferenceGenerator>();
builder.Services.AddSingleton<IBookingEventPublisher, LoggingBookingEventPublisher>();
builder.Services.AddControllers();
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

// Authentication must precede Authorization in the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();