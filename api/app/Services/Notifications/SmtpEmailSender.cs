using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace SeniorCare.Api.App.Services.Notifications;

public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public bool IsConfigured => options.IsConfigured;

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "El envío de correos no está configurado (SMTP_ENABLED, SMTP_USER y SMTP_PASSWORD).");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false
        };

        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.HtmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.User, options.Password),
            Timeout = 20_000
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}
