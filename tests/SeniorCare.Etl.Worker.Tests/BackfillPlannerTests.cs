using SeniorCare.Etl.Worker;

namespace SeniorCare.Etl.Worker.Tests;

public sealed class BackfillPlannerTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Fact]
    public void ResolveRange_StartsAtDataStartAndEndsYesterday()
    {
        var range = BackfillPlanner.ResolveRange(Today, new DateOnly(2026, 10, 28), windowDays: 30);

        Assert.Equal((new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29)), range);
    }

    [Fact]
    public void ResolveRange_IsLimitedByTheWindow()
    {
        var range = BackfillPlanner.ResolveRange(Today, new DateOnly(2025, 1, 1), windowDays: 7);

        Assert.Equal((new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 29)), range);
    }

    [Fact]
    public void ResolveRange_NeverIncludesToday()
    {
        Assert.Null(BackfillPlanner.ResolveRange(Today, Today, windowDays: 30));
    }

    [Fact]
    public void ResolveRange_WithoutSourceData_ReturnsNull()
    {
        Assert.Null(BackfillPlanner.ResolveRange(Today, dataStart: null, windowDays: 30));
    }

    [Fact]
    public void DaysWithoutSourceData_CountsWindowDaysBeforeFirstData()
    {
        Assert.Equal(28, BackfillPlanner.DaysWithoutSourceData(Today, new DateOnly(2026, 10, 28), windowDays: 30));
        Assert.Equal(0, BackfillPlanner.DaysWithoutSourceData(Today, new DateOnly(2025, 1, 1), windowDays: 30));
        Assert.Equal(30, BackfillPlanner.DaysWithoutSourceData(Today, dataStart: null, windowDays: 30));
    }

    [Fact]
    public void PendingDates_AreChronologicalSkipExistingAndRespectBatch()
    {
        var existing = new HashSet<DateOnly> { new(2026, 10, 21), new(2026, 10, 23) };

        var pending = BackfillPlanner.PendingDates(
            new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 29),
            existing,
            batchDays: 3);

        Assert.Equal(
            [new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 24)],
            pending);
    }

    [Fact]
    public void CountMissing_IgnoresExistingDatesOutsideTheRange()
    {
        var existing = new HashSet<DateOnly> { new(2026, 10, 25), new(2026, 9, 1) };

        Assert.Equal(
            9,
            BackfillPlanner.CountMissing(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 29), existing));
    }
}
