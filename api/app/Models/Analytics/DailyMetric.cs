namespace SeniorCare.Api.App.Models.Analytics;

public sealed record DailyMetric(
    DateOnly MetricDate,
    int ScheduledDoses,
    int TakenDoses,
    int AssistanceAlerts,
    int EmergencyAlerts,
    int ResolvedAlerts,
    DateTimeOffset EtlRunAt)
{
    public decimal AdherencePercentage =>
        ScheduledDoses == 0
            ? 0
            : Math.Round((decimal)TakenDoses / ScheduledDoses * 100, 1);
}

