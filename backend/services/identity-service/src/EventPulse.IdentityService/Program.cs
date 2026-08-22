using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Identity & User Service
// ---------------------------------------------------------------------------
// Owns: user registration, user profiles, authentication (JWT later),
//       Google OAuth later.
// Does NOT reference: EventService, BookingService, PaymentService.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDatabase")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

// Health endpoint consumed by YARP gateway health-check and load balancer
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
