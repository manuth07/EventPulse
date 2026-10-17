using System.Security.Claims;
using System.Text;
using EventPulse.BookingService;
using EventPulse.BookingService.Consumers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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
            new SecurityKey[] { keyedSigningKey, unkeyedSigningKey },
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.OrganizerOnly, policy => policy.RequireRole("Organizer"));
    options.AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole("Administrator"));
    options.AddPolicy(AppPolicies.AdministratorOnly, policy => policy.RequireRole("Administrator"));
});
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IBookingReferenceGenerator, BookingReferenceGenerator>();
builder.Services.AddScoped<IBookingConfirmationService, BookingConfirmationService>();
builder.Services.AddScoped<IBookingPaymentFailureService, BookingPaymentFailureService>();
builder.Services.AddScoped<ITicketCodeGenerator, TicketCodeGenerator>();
builder.Services.AddScoped<IValidationTokenGenerator, ValidationTokenGenerator>();
builder.Services.AddScoped<ITicketGenerationService, TicketGenerationService>();
builder.Services.AddScoped<IBookingHistoryService, BookingHistoryService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICancellationEligibilityChecker, CancellationEligibilityChecker>();
builder.Services.AddScoped<IBookingCancellationService, BookingCancellationService>();
builder.Services.AddSingleton<IBookingEventPublisher, LoggingBookingEventPublisher>();
builder.Services.AddScoped<ISalesReportingService, SalesReportingService>();
builder.Services.AddScoped<IOrganizerSalesAnalyticsService, OrganizerSalesAnalyticsService>();
builder.Services.AddScoped<IOrganizerBookingAnalyticsService, OrganizerBookingAnalyticsService>();
builder.Services.AddScoped<IReportFilteringService, ReportFilteringService>();
builder.Services.AddScoped<IReportDataAggregationService, ReportDataAggregationService>();

// ---------------------------------------------------------------------------
// Kafka Event Consumers & Handlers
// ---------------------------------------------------------------------------
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();
builder.Services.AddScoped<IPaymentSucceededEventHandler, PaymentSucceededEventHandler>();
builder.Services.AddScoped<IPaymentFailedEventHandler, PaymentFailedEventHandler>();
builder.Services.AddScoped<IPaymentRefundedEventHandler, PaymentRefundedEventHandler>();
builder.Services.AddScoped<IKafkaPaymentEventDispatcher, KafkaPaymentEventDispatcher>();
builder.Services.AddHostedService<KafkaPaymentEventConsumer>();
builder.Services.AddHostedService<EventPulse.BookingService.BackgroundServices.ExpiredBookingCleanupWorker>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EventPulse Booking Service API",
        Version = "v1",
        Description = "Booking, cart, and reservation microservice for EventPulse."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseRouting();

// Prometheus HTTP metrics middleware
app.UseHttpMetrics();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "EventPulse Booking Service API v1");
    options.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

// Middleware Ordering: CORS -> Authentication -> Authorization -> Endpoints
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint
app.MapMetrics();

app.MapControllers();

await app.RunAsync();