using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// CORS Policy
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// YARP Reverse Proxy
// ---------------------------------------------------------------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ---------------------------------------------------------------------------
// OpenAPI & Health Checks
// ---------------------------------------------------------------------------
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

var app = builder.Build();

// Prometheus HTTP Request Metrics (TECH-12)
app.UseRouting();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFrontend");

// Health endpoint for the gateway (resolved locally)
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint for Gateway metrics (resolved locally before proxy)
app.MapMetrics();

// Map all configured YARP routes downstream
app.MapReverseProxy();

app.Run();