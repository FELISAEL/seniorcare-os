using SeniorCare.Api.App.Models.Analytics;
using SeniorCare.Api.App.Repositories.Analytics;

namespace SeniorCare.Api.App.Services.Analytics;

public sealed class AnalyticsService(IAnalyticsRepository analytics)
{
    public Task<DailyMetric?> FindAsync(
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        analytics.FindAsync(date, cancellationToken);

    public Task<IReadOnlyList<DailyMetric>> ListRecentAsync(
        int? days,
        CancellationToken cancellationToken = default)
    {
        var range = Math.Clamp(days ?? 7, 1, 31);
        return analytics.ListRecentAsync(range, cancellationToken);
    }
}
