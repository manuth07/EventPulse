namespace EventPulse.E2E.Tests;

public static class Constants
{
    // Adjust if your dev server runs on a different port.
    public const string BaseUrl = "http://localhost:5173";
    // ASSUMPTION: I haven't seen your router config, only Register.tsx.
    // Confirm this matches the actual <Route path="..."> for the Register component.
    public const string RegisterPath = "/register";
    public const string LoginPath = "/login";
    public const string VerifyEmailPath = "/verify-email";
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);
}