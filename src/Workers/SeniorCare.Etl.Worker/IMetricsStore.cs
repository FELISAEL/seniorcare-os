namespace SeniorCare.Etl.Worker;

/// <summary>Acceso a las bases de origen (lectura) y a analytics_db.daily_metrics (escritura).</summary>
public interface IMetricsStore
{
    /// <summary>Crea la tabla si falta y aplica los cambios aditivos (scheduled_doses nulo, data_quality).</summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken);

    /// <summary>Tomas diarias del plan vigente en este momento. Solo es válido para el día actual.</summary>
    Task<int> CountCurrentScheduledDosesAsync(CancellationToken cancellationToken);

    Task<DailyFacts> ExtractFactsAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Inserta o actualiza la fila completa del día actual (comportamiento periódico).</summary>
    Task UpsertCompleteAsync(DateOnly date, int scheduledDoses, DailyFacts facts, CancellationToken cancellationToken);

    /// <summary>
    /// Cierre del día anterior: actualiza tomas y alertas de una fila completa cuya última
    /// ejecución fue antes de que terminara ese día; conserva las tomas programadas registradas.
    /// </summary>
    Task<bool> CloseCompleteDayAsync(DateOnly date, DateTimeOffset dayEnd, DailyFacts facts, CancellationToken cancellationToken);

    Task<bool> NeedsClosingAsync(DateOnly date, DateTimeOffset dayEnd, CancellationToken cancellationToken);

    /// <summary>Primera fecha local con datos de origen (residentes, alertas o tomas). Null si no hay datos.</summary>
    Task<DateOnly?> FindDataStartAsync(CancellationToken cancellationToken);

    Task<IReadOnlySet<DateOnly>> ListMetricDatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Inserta una fila parcial solo si la fecha no tiene métricas. Devuelve false si ya existía.</summary>
    Task<bool> InsertPartialIfMissingAsync(DateOnly date, DailyFacts facts, CancellationToken cancellationToken);

    /// <summary>Bloqueo exclusivo de la recuperación histórica. Null si otra recuperación lo tiene.</summary>
    Task<IAsyncDisposable?> TryAcquireBackfillLockAsync(CancellationToken cancellationToken);
}
