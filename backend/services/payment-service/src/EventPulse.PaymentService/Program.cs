using System.Security.Claims;
using System.Text;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Payment Service
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDatabase")));

builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BookingService:BaseUrl"] ?? "http://localhost:7103");
});

builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var keyString = jwtSection["Key"] ?? "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";
var keyBytes = Encoding.UTF8.GetBytes(keyString);
var signingKey = new SymmetricSecurityKey(keyBytes) { KeyId = "EventPulseKey_2026" };
var unkeyedSigningKey = new SymmetricSecurityKey(keyBytes);

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
            new SecurityKey[] { signingKey, unkeyedSigningKey },
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"] ?? "EventPulse.IdentityService",
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"] ?? "EventPulse.Clients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
});

builder.Services.AddAuthorization();
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
else
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.MigrateAsync();
}

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

// Health endpoint for YARP / Kubernetes / Azure probes
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint (TECH-12)
app.MapMetrics();

app.MapControllers();

await app.RunAsync();