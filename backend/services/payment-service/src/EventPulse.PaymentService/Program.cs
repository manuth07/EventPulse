using System.Text;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Payment Service
// ---------------------------------------------------------------------------
// Owns: payment transactions, Stripe checkout sessions, payment status.
// Communicates with BookingService (:7103) for authoritative booking summaries.
// ---------------------------------------------------------------------------

// 1. Database Persistence
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDatabase")));

// 2. Stripe SDK Initialization
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// 3. HTTP Clients & Domain Services
builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BookingService:BaseUrl"] ?? "http://localhost:7103");
});

builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();

// 4. JWT Bearer Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKeyStr = jwtSection["Key"];
var jwtKeyBytes = !string.IsNullOrEmpty(jwtKeyStr)
    ? Encoding.UTF8.GetBytes(jwtKeyStr)
    : new byte[32];

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
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database Migration on Startup
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not apply database migrations on startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
