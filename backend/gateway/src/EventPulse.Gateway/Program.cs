var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// YARP Reverse Proxy
// ---------------------------------------------------------------------------
// ReverseProxy reads its configuration from appsettings.json (ReverseProxy
// section). No business logic, no DB access, no controllers needed here.
// The Gateway is ONLY a routing/proxy layer.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ---------------------------------------------------------------------------
// OpenAPI / Swagger (development only – useful to inspect gateway routes)
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// Health checks endpoint for the gateway itself
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health endpoint for the gateway (not proxied, resolved locally)
app.MapHealthChecks("/health");

// Map all configured YARP routes
app.MapReverseProxy();

app.Run();
