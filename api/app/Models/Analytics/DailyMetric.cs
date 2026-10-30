namespace SeniorCare.Api.App.Models.Analytics;

/// <summary>
/// Métricas de un día. DataQuality "complete": calculadas ese mismo día con el plan vigente.
/// "partial": reconstruidas después; tomas y alertas confirmadas, plan programado desconocido
/// (ScheduledDoses y AdherencePercentage nulos, nunca un cero estimado).
/// </summary>
public sealed record DailyMetric(
    DateOnly MetricDate,
    int? ScheduledDoses,
    int TakenDoses,
    int AssistanceAlerts,
    int EmergencyAlerts,
    int ResolvedAlerts,
    DateTimeOffset EtlRunAt,
    string DataQuality = "complete")
{
    public decimal? AdherencePercentage =>
        ScheduledDoses switch
        {
            null => null,
            0 => 0,
            var scheduled => Math.Round((decimal)TakenDoses / scheduled.Value * 100, 1)
        };
}
