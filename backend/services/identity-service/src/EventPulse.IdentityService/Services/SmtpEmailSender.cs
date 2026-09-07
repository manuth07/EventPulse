using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// SMTP implementation for sending transactional emails.
/// Uses MailKit for reliable delivery.
/// Requires valid SMTP credentials in configuration.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = "Verify your EventPulse email";

            // Professional, simple text/HTML layout per requirements
            var textBody = $@"EventPulse

Verify your email

Use the verification code below to finish creating your EventPulse account.

{code}

This code expires in 10 minutes.

If you didn't create an EventPulse account, you can ignore this email.
";

            var htmlBody = $@"
<div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 500px; margin: 0 auto; padding: 30px 20px; color: #1D1D1F;"">
    <h2 style=""margin-bottom: 20px; font-weight: 700; color: #1D1D1F;"">
        Event<span style=""color: #FF5B00;"">Pulse</span>
    </h2>
    
    <h3 style=""font-size: 20px; font-weight: 600; margin-bottom: 12px;"">Verify your email</h3>
    
    <p style=""font-size: 15px; line-height: 1.5; color: #1D1D1F; margin-bottom: 24px;"">
        Use the verification code below to finish creating your EventPulse account.
    </p>
    
    <div style=""background-color: #F5F5F7; border-radius: 12px; padding: 20px; text-align: center; margin-bottom: 24px;"">
        <span style=""font-size: 32px; font-weight: 700; letter-spacing: 4px; color: #1D1D1F;"">{code}</span>
    </div>
    
    <p style=""font-size: 14px; color: #86868B; margin-bottom: 30px;"">
        This code expires in 10 minutes.
    </p>
    
    <hr style=""border: none; border-top: 1px solid #E5E5EA; margin-bottom: 24px;"" />
    
    <p style=""font-size: 13px; color: #86868B; line-height: 1.5;"">
        If you didn't create an EventPulse account, you can safely ignore this email.
    </p>
</div>
";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = textBody,
                HtmlBody = htmlBody
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect
            var secureSocketOptions = _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions);
            
            // Authenticate if credentials provided
            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }
            
            // Send
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Verification email successfully sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            // Log failure but don't expose secrets or passwords
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw; // Re-throw to inform caller the delivery failed
        }
    }
}
