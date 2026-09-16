using System.Security.Claims;
using System.Text;
using EventPulse.EventService;
using EventPulse.EventService.Configuration;
using EventPulse.EventService.Data;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EventDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EventDatabase")));

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.OrganizerOnly, policy => policy.RequireRole("Organizer"));
    options.AddPolicy(AppPolicies.AdministratorOnly, policy => policy.RequireRole("Administrator"));
});

builder.Services.AddScoped<IEventSubmissionService, EventSubmissionService>();
builder.Services.AddScoped<IEventReviewService, EventReviewService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
builder.Services.AddScoped<IEventUpdateRequestService, EventUpdateRequestService>();
builder.Services.AddScoped<IEventCancellationRequestService, EventCancellationRequestService>();
builder.Services.AddSingleton<IEventImageStorage, AzureBlobEventImageStorage>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

var app = builder.Build();

app.UseRouting();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var databaseSettings = app.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>() ?? new DatabaseSettings();

if (app.Environment.IsDevelopment() || databaseSettings.MigrateOnStartup || databaseSettings.SeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

    if (app.Environment.IsDevelopment() || databaseSettings.MigrateOnStartup)
    {
        await dbContext.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment() || databaseSettings.SeedOnStartup)
    {
        await EventDbSeeder.SeedAsync(dbContext);
    }
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapMetrics();
app.MapControllers();

await app.RunAsync();