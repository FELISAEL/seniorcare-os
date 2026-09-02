using SeniorCare.Api.App.Controllers.Emergency;
using SeniorCare.Api.App.Core.Data;
using SeniorCare.Api.App.Repositories.Emergency;
using SeniorCare.Api.App.Services.Emergency;

namespace SeniorCare.Api.Config;

public static class EmergencyModule
{
    public static IServiceCollection AddEmergencyModule(
        this IServiceCollection services)
    {
        services.AddSingleton<EmergencyRepository>();

        services.AddSingleton<IEmergencyRepository>(provider =>
            provider.GetRequiredService<EmergencyRepository>());

        services.AddSingleton<AlertService>();

        return services;
    }

    public static async Task InitializeEmergencyModuleAsync(
        this WebApplication app)
    {
        var repository =
            app.Services.GetRequiredService<EmergencyRepository>();

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("EmergencyDatabaseInitialization");

        await DatabaseRetry.ExecuteAsync(
            () => repository.InitializeAsync(
                app.Lifetime.ApplicationStopping),
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static IEndpointRouteBuilder MapEmergencyModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAlertsController();

        return endpoints;
    }
}