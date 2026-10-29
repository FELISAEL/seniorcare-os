using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Services.Notifications;

namespace SeniorCare.Api.App.Controllers.Notifications;

public sealed record TestEmailRequest(string? Email);

public static class NotificationsController
{
    public static IEndpointRouteBuilder MapNotificationsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Notificaciones");

        group.MapGet("/status", (IEmailSender sender) =>
                Results.Ok(new { configured = sender.IsConfigured }))
            .RequireAuthorization(SeniorCarePolicies.AdminOnly);

        group.MapPost("/test", SendTestAsync)
            .RequireAuthorization(SeniorCarePolicies.AdminOnly);

        return endpoints;
    }

    private static async Task<IResult> SendTestAsync(
        TestEmailRequest request,
        IEmailSender sender,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !email.Contains('@') || email.Length > 254)
        {
            return Results.BadRequest(new { message = "Escribe un correo válido." });
        }

        if (!sender.IsConfigured)
        {
            return Results.BadRequest(new
            {
                message = "El envío de correos no está configurado. Revisa SMTP_ENABLED, SMTP_USER y SMTP_PASSWORD en el archivo .env."
            });
        }

        try
        {
            await sender.SendAsync(EmailTemplates.Test(email), cancellationToken);
            return Results.Ok(new { message = $"Correo de prueba enviado a {email}." });
        }
        catch (Exception exception)
        {
            loggers.CreateLogger("Notifications").LogWarning(exception, "Falló el correo de prueba.");
            return Results.Json(
                new { message = "No se pudo enviar el correo. Verifica la contraseña de aplicación y la cuenta de Gmail." },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
