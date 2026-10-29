using SeniorCare.Api.App.Controllers.Notifications;
using SeniorCare.Api.App.Services.Notifications;

namespace SeniorCare.Api.Config;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            EmailOptions.FromConfiguration(
                provider.GetRequiredService<IConfiguration>()));

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<FamilyNotificationService>();

        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapNotificationsController();

        return endpoints;
    }
}
