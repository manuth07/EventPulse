using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Event Service
// ---------------------------------------------------------------------------
// Owns: events, categories, venues, schedules.
// Does NOT reference: IdentityService, BookingService, PaymentService.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<EventDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EventDatabase")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();
        await EventDbSeeder.SeedAsync(dbContext);
    }
}

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
