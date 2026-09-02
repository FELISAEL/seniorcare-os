using SeniorCare.Api.App.Controllers.Analytics;
using SeniorCare.Api.App.Core.Data;
using SeniorCare.Api.App.Repositories.Analytics;
using SeniorCare.Api.App.Services.Analytics;

namespace SeniorCare.Api.Config;

public static class AnalyticsModule
{
    public static IServiceCollection AddAnalyticsModule(
        this IServiceCollection services)
    {
        services.AddSingleton<AnalyticsRepository>();

        services.AddSingleton<IAnalyticsRepository>(provider =>
            provider.GetRequiredService<AnalyticsRepository>());

        services.AddSingleton<AnalyticsService>();

        return services;
    }

    public static async Task InitializeAnalyticsModuleAsync(
        this WebApplication app)
    {
        var repository =
            app.Services.GetRequiredService<AnalyticsRepository>();

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AnalyticsDatabaseInitialization");

        await DatabaseRetry.ExecuteAsync(
            () => repository.InitializeAsync(
                app.Lifetime.ApplicationStopping),
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static IEndpointRouteBuilder MapAnalyticsModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMetricsController();

        return endpoints;
    }
}