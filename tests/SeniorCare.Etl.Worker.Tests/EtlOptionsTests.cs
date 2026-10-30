using Microsoft.Extensions.Configuration;
using SeniorCare.Etl.Worker;

namespace SeniorCare.Etl.Worker.Tests;

public sealed class EtlOptionsTests
{
    [Fact]
    public void Defaults_KeepPeriodicBehaviourAndBackfillDisabled()
    {
        var options = EtlOptions.FromConfiguration(Configuration());

        Assert.Equal(TimeSpan.FromSeconds(30), options.Interval);
        Assert.Equal("America/Guatemala", options.TimeZone.Id);
        Assert.False(options.BackfillEnabled);
        Assert.Equal(30, options.BackfillDays);
        Assert.Equal(7, options.BackfillBatchDays);
    }

    [Fact]
    public void EnvironmentVariables_AreReadAndClamped()
    {
        var options = EtlOptions.FromConfiguration(Configuration(
            ("ETL_INTERVAL_SECONDS", "1"),
            ("ETL_BACKFILL_ENABLED", "true"),
            ("ETL_BACKFILL_DAYS", "9999"),
            ("ETL_BACKFILL_BATCH_DAYS", "0")));

        Assert.Equal(TimeSpan.FromSeconds(10), options.Interval);
        Assert.True(options.BackfillEnabled);
        Assert.Equal(365, options.BackfillDays);
        Assert.Equal(1, options.BackfillBatchDays);
    }

    [Fact]
    public void LocalDate_UsesConfiguredZoneAroundMidnight()
    {
        var options = EtlOptions.FromConfiguration(Configuration());

        // 05:30 UTC del 30 de octubre son las 23:30 del 29 en Guatemala (UTC-6).
        Assert.Equal(
            new DateOnly(2026, 10, 29),
            options.LocalDate(new DateTimeOffset(2026, 10, 30, 5, 30, 0, TimeSpan.Zero)));
        Assert.Equal(
            new DateOnly(2026, 10, 30),
            options.LocalDate(new DateTimeOffset(2026, 10, 30, 6, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void StartOfDay_IsLocalMidnight()
    {
        var options = EtlOptions.FromConfiguration(Configuration());

        Assert.Equal(
            new DateTimeOffset(2026, 10, 30, 6, 0, 0, TimeSpan.Zero),
            options.StartOfDay(new DateOnly(2026, 10, 30)).ToUniversalTime());
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
