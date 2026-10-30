namespace SeniorCare.Etl.Worker;

/// <summary>
/// Hechos registrados de una fecha local, leídos de los eventos de origen:
/// tomas confirmadas (dose_events) y alertas creadas ese día (alerts.created_at).
/// No incluye las tomas programadas, porque el plan de medicamentos no guarda historial.
/// </summary>
public sealed record DailyFacts(
    int TakenDoses,
    int AssistanceAlerts,
    int EmergencyAlerts,
    int ResolvedAlerts);

/// <summary>Calidad de una fila de daily_metrics.</summary>
public static class DataQuality
{
    /// <summary>Calculada el mismo día por el ETL periódico, con el plan vigente ese día.</summary>
    public const string Complete = "complete";

    /// <summary>Reconstruida después: tomas y alertas confirmadas, plan programado desconocido (NULL).</summary>
    public const string Partial = "partial";
}
