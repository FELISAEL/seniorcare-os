using SeniorCare.Api.App.Models.Analytics;
using SeniorCare.Api.App.Repositories.Analytics;
using SeniorCare.Api.App.Services.Analytics;

namespace SeniorCare.Api.Tests.Services.Analytics;

public sealed class AnalyticsServiceTests
{
    [Theory]
    [InlineData(null, 7)]
    [InlineData(0, 1)]
    [InlineData(15, 15)]
    [InlineData(100, 31)]
    public async Task ListRecentAsync_NormalizesRequestedDays(
        int? requestedDays,
        int expectedDays)
    {
        var repository = new FakeAnalyticsRepository();
        var service = new AnalyticsService(repository);

        await service.ListRecentAsync(requestedDays);

        Assert.Equal(expectedDays, repository.LastDays);
    }

    [Fact]
    public async Task FindAsync_ForwardsRequestedDate()
    {
        var repository = new FakeAnalyticsRepository();
        var service = new AnalyticsService(repository);
        var date = new DateOnly(2026, 9, 2);

        await service.FindAsync(date);

        Assert.Equal(date, repository.LastDate);
    }

    [Fact]
    public void AdherencePercentage_WithScheduledDoses_CalculatesPercentage()
    {
        var metric = CreateMetric(
            scheduledDoses: 2,
            takenDoses: 1);

        Assert.Equal(50.0m, metric.AdherencePercentage);
    }

    [Fact]
    public void AdherencePercentage_WithoutScheduledDoses_ReturnsZero()
    {
        var metric = CreateMetric(
            scheduledDoses: 0,
            takenDoses: 0);

        Assert.Equal(0m, metric.AdherencePercentage);
    }

    private static DailyMetric CreateMetric(
        int scheduledDoses,
        int takenDoses) =>
        new(
            new DateOnly(2026, 9, 2),
            scheduledDoses,
            takenDoses,
            0,
            0,
            0,
            DateTimeOffset.UtcNow);

    private sealed class FakeAnalyticsRepository : IAnalyticsRepository
    {
        public int? LastDays { get; private set; }
        public DateOnly? LastDate { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<DailyMetric?> FindAsync(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            LastDate = date;
            return Task.FromResult<DailyMetric?>(null);
        }

        public Task<IReadOnlyList<DailyMetric>> ListRecentAsync(
            int days,
            CancellationToken cancellationToken = default)
        {
            LastDays = days;

            return Task.FromResult<IReadOnlyList<DailyMetric>>(
                Array.Empty<DailyMetric>());
        }
    }
}
