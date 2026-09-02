using SeniorCare.Api.App.Controllers.Care;
using SeniorCare.Api.App.Core.Data;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Services.Care;

namespace SeniorCare.Api.Config;

public static class CareModule
{
    public static IServiceCollection AddCareModule(
        this IServiceCollection services)
    {
        services.AddSingleton<CareRepository>();

        services.AddSingleton<ICareRepository>(provider =>
            provider.GetRequiredService<CareRepository>());

        services.AddSingleton<ResidentAccessService>();
        services.AddSingleton<ResidentService>();
        services.AddSingleton<MedicationService>();

        return services;
    }

    public static async Task InitializeCareModuleAsync(
        this WebApplication app)
    {
        var repository =
            app.Services.GetRequiredService<CareRepository>();

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CareDatabaseInitialization");

        await DatabaseRetry.ExecuteAsync(
            () => repository.InitializeAsync(
                app.Lifetime.ApplicationStopping),
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static IEndpointRouteBuilder MapCareModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapResidentsController();
        endpoints.MapMedicationsController();

        return endpoints;
    }
}