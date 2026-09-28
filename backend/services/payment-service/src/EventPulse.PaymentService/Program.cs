using System.Security.Claims;
using System.Text;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Payment Service
// Owns: payment transactions, refunds, payment status.
// Does NOT reference: IdentityService, EventService, BookingService.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDatabase")));

builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BookingService:BaseUrl"] ?? "http://localhost:7103");
});

builder.Services.AddScoped<IStripeCheckoutService, StripeCheckoutService>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<IKafkaMessageProducer, KafkaMessageProducer>();
builder.Services.AddHostedService<OutboxPublisherService>();

var stripeSecretKey = builder.Configuration["STRIPE_SECRET_KEY"]
    ?? builder.Configuration["Stripe__SecretKey"]
    ?? builder.Configuration["Stripe:SecretKey"]
    ?? builder.Configuration.GetSection("Stripe")["SecretKey"];

StripeConfiguration.ApiKey = stripeSecretKey;

if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    Console.WriteLine("[STRIPE-INIT] Stripe API configuration detected.");
}
else
{
    Console.WriteLine("[STRIPE-INIT] Stripe API key is not configured.");
}

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
            new SecurityKey[] { signingKey, unkeyedSigningKey },
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
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
    {
        // Relation already exists in local dev environment
        app.Logger.LogWarning("Payments table already exists. Skipping creation.");
    }
    catch (Exception ex) when (ex.InnerException is Npgsql.PostgresException inner && inner.SqlState == "42P07")
    {
        // Relation already exists in local dev environment
        app.Logger.LogWarning("Payments table already exists. Skipping creation.");
    }
    catch (Exception ex) when (ex.Message.Contains("42P07") || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogWarning("Payments table already exists. Skipping creation: {Message}", ex.Message);
    }
}

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

await app.RunAsync();
