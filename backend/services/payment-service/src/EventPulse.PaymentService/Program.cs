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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
