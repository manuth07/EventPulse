namespace EventPulse.E2E.Tests;

public static class Constants
{
    // BaseUrl defaults to http://localhost:5173 but can be overridden via EVENTPULSE_BASE_URL environment variable.
    public static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("EVENTPULSE_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:5173";

    // Route paths
    public const string RegisterPath = "/register";
    public const string LoginPath = "/login";
    public const string VerifyEmailPath = "/verify-email";
    public const string MyBookingsPath = "/my-bookings";

    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);
}