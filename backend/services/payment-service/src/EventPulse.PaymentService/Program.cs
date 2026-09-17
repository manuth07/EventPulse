using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Payment Service
// ---------------------------------------------------------------------------
// Owns: payment transactions, refunds, payment status.
// Does NOT reference: IdentityService, EventService, BookingService.
// Receives booking events via Kafka (later) → processes payment → publishes
// payment-confirmed or payment-failed events back to Kafka.
// ---------------------------------------------------------------------------
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

app.UseAuthorization();

// Health endpoint for YARP / Kubernetes / Azure probes
app.MapHealthChecks("/health");

// Prometheus Scrape Endpoint (TECH-12)
app.MapMetrics();

app.MapControllers();

app.Run();