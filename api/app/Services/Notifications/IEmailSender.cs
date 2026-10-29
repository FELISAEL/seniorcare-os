namespace SeniorCare.Api.App.Services.Notifications;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
