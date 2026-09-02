using SeniorCare.Api.App.Services.Analytics;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Controllers.Analytics;

public static class MetricsController
{
    public static IEndpointRouteBuilder MapMetricsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/analytics")
            .WithTags("Analítica")
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet("/daily/{date}", async (
            DateOnly date,
            AnalyticsService analytics,
            CancellationToken cancellationToken) =>
        {
            var metric = await analytics.FindAsync(date, cancellationToken);
            return metric is null
                ? Results.NotFound(new
                {
                    message = "El ETL todavía no ha generado métricas para esa fecha."
                })
                : Results.Ok(metric);
        });

        group.MapGet("/recent", async (
            int? days,
            AnalyticsService analytics,
            CancellationToken cancellationToken) =>
            Results.Ok(await analytics.ListRecentAsync(days, cancellationToken)));

        return endpoints;
    }
}
