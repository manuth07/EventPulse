var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Booking Service
// ---------------------------------------------------------------------------
// Owns: ticket reservations, seat availability, booking lifecycle.
// Does NOT reference: IdentityService, EventService, PaymentService.
// Communicates with EventService (read event/seat data) → via HTTP client later.
// Communicates with PaymentService (payment confirmation) → via Kafka later.
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
