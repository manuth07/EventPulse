using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Payment Service
// ---------------------------------------------------------------------------
// Owns: payment transactions, refunds, payment status.
// Does NOT reference: IdentityService, EventService, BookingService.
// ---------------------------------------------------------------------------

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

// Middleware Ordering: CORS -> Authentication -> Authorization -> Endpoints
app.UseAuthentication();
app.UseAuthorization();

// Health endpoint for YARP / Kubernetes / Azure probes
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint (TECH-12)
app.MapMetrics();

app.MapControllers();

app.Run();