namespace EventPulse.IdentityService.Services;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string toEmail, string code);
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, int expiryMinutes);
}
