using AracParki.Application.Abstractions;
using AracParki.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AracParki.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailSettings> settings,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost)
            || string.IsNullOrWhiteSpace(_settings.FromEmail))
        {
            throw new InvalidOperationException("EmailSettings is not configured (SmtpHost / FromEmail).");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        // macOS/.NET often fails OCSP/CRL ("incomplete certificate revocation check") against
        // public SMTP relays; chain trust is still validated.
        client.CheckCertificateRevocation = false;
        try
        {
            var secure = _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secure, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            logger.LogInformation("Email sent to {ToEmail} subject {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {ToEmail} subject {Subject}", toEmail, subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
