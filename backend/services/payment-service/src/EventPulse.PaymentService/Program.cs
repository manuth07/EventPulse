using System.Security.Claims;
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
// Does NOT reference: IdentityService, EventService.
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
// 4. JWT Bearer Authentication — validates tokens issued by Identity Service
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

var signingKey = new SymmetricSecurityKey(jwtKeyBytes)
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
        IssuerSigningKey = signingKey,
        IssuerSigningKeys = new SecurityKey[] { signingKey, unkeyedSigningKey },
        IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
        {
            return new SecurityKey[] { signingKey, unkeyedSigningKey };
        },
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? builder.Configuration["Jwt:Issuer"] ?? "EventPulse.IdentityService",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? builder.Configuration["Jwt:Audience"] ?? "EventPulse.Clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("PaymentService.JwtAuthentication");
            logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("PaymentService.JwtAuthentication");
            logger.LogWarning("JWT challenge triggered: Error={Error}, Description={Description}",
                context.Error, context.ErrorDescription);
            return Task.CompletedTask;
        }
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

app.UseCors("AllowAll");

// Middleware Ordering: CORS -> Authentication -> Authorization -> Endpoints
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
