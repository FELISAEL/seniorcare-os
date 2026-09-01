using SeniorCare.Analytics.Api.Domain;

namespace SeniorCare.Analytics.Api.Repositories;

public interface IAnalyticsRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<DailyMetric?> FindAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyMetric>> ListRecentAsync(int days, CancellationToken cancellationToken = default);
}
