using SeniorCare.Api.App.Models.Analytics;

namespace SeniorCare.Api.App.Repositories.Analytics;

public interface IAnalyticsRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<DailyMetric?> FindAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyMetric>> ListRecentAsync(int days, CancellationToken cancellationToken = default);
}
