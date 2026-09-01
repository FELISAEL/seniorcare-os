using SeniorCare.Analytics.Api.Domain;
using SeniorCare.Analytics.Api.Repositories;

namespace SeniorCare.Analytics.Api.Services.Metrics;

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
