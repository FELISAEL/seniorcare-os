namespace SeniorCare.Etl.Worker;

/// <summary>Resultado de la recuperación histórica de un ciclo (para registros y pruebas).</summary>
public sealed record BackfillResult(
    IReadOnlyList<DateOnly> Written,
    IReadOnlyList<DateOnly> AlreadyPresent,
    IReadOnlyList<DateOnly> Failed,
    int WithoutSourceData,
    int RemainingMissing,
    bool SkippedByLock)
{
    public static readonly BackfillResult Disabled = new([], [], [], 0, 0, false);
}

/// <summary>
/// Un ciclo del ETL: métricas completas del día actual, cierre del día anterior y, si está
/// habilitada, un lote de recuperación histórica. Cada parte registra sus propios errores.
/// </summary>
public sealed class MetricsProcessor(
    IMetricsStore store,
    EtlOptions options,
    TimeProvider clock,
    ILogger<MetricsProcessor> logger)
{
    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = options.LocalDate(now);

        await RunTodayAsync(today, cancellationToken);
        await CloseYesterdayAsync(today, cancellationToken);

        if (options.BackfillEnabled)
        {
            await RunBackfillAsync(today, cancellationToken);
        }
    }

    /// <summary>Comportamiento periódico original: el plan vigente es el plan de hoy.</summary>
    public async Task RunTodayAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var scheduled = await store.CountCurrentScheduledDosesAsync(cancellationToken);
        var facts = await store.ExtractFactsAsync(today, cancellationToken);
        await store.UpsertCompleteAsync(today, scheduled, facts, cancellationToken);

        logger.LogInformation(
            "ETL completado para {Date}: {Taken}/{Scheduled} tomas y {Alerts} alertas.",
            today,
            facts.TakenDoses,
            scheduled,
            facts.AssistanceAlerts + facts.EmergencyAlerts);
    }

    /// <summary>
    /// Tras la medianoche, recoge las tomas y alertas de los últimos segundos del día anterior.
    /// Solo una vez por día y solo en filas completas; el plan registrado ese día se conserva.
    /// </summary>
    public async Task<bool> CloseYesterdayAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var yesterday = today.AddDays(-1);
        var dayEnd = options.StartOfDay(today);
        try
        {
            if (!await store.NeedsClosingAsync(yesterday, dayEnd, cancellationToken))
            {
                return false;
            }

            var facts = await store.ExtractFactsAsync(yesterday, cancellationToken);
            var closed = await store.CloseCompleteDayAsync(yesterday, dayEnd, facts, cancellationToken);
            if (closed)
            {
                logger.LogInformation("Día {Date} cerrado con sus tomas y alertas finales.", yesterday);
            }

            return closed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "No se pudo cerrar el día {Date}; se reintentará.", yesterday);
            return false;
        }
    }

    public async Task<BackfillResult> RunBackfillAsync(DateOnly today, CancellationToken cancellationToken)
    {
        await using var backfillLock = await store.TryAcquireBackfillLockAsync(cancellationToken);
        if (backfillLock is null)
        {
            logger.LogInformation("Recuperación histórica omitida: otra recuperación está en curso.");
            return BackfillResult.Disabled with { SkippedByLock = true };
        }

        var dataStart = await store.FindDataStartAsync(cancellationToken);
        var withoutData = BackfillPlanner.DaysWithoutSourceData(today, dataStart, options.BackfillDays);
        var range = BackfillPlanner.ResolveRange(today, dataStart, options.BackfillDays);
        if (range is null)
        {
            if (withoutData > 0)
            {
                logger.LogInformation(
                    "Recuperación histórica: {Days} días de la ventana no tienen datos de origen y no se escriben.",
                    withoutData);
            }

            return BackfillResult.Disabled with { WithoutSourceData = withoutData };
        }

        var (from, to) = range.Value;
        var existing = await store.ListMetricDatesAsync(from, to, cancellationToken);
        var pending = BackfillPlanner.PendingDates(from, to, existing, options.BackfillBatchDays);

        var written = new List<DateOnly>();
        var present = new List<DateOnly>();
        var failed = new List<DateOnly>();
        foreach (var date in pending)
        {
            try
            {
                var facts = await store.ExtractFactsAsync(date, cancellationToken);
                if (await store.InsertPartialIfMissingAsync(date, facts, cancellationToken))
                {
                    written.Add(date);
                    logger.LogInformation(
                        "Recuperada {Date} (parcial): {Taken} tomas confirmadas, {Assistance} asistencias, {Emergency} SOS; plan programado no disponible.",
                        date,
                        facts.TakenDoses,
                        facts.AssistanceAlerts,
                        facts.EmergencyAlerts);
                }
                else
                {
                    present.Add(date);
                    logger.LogInformation("Fecha {Date} omitida: ya tenía métricas.", date);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Un fallo no detiene el lote: la fecha sigue pendiente y se reintenta en el próximo ciclo.
                failed.Add(date);
                logger.LogError(exception, "No se pudo recuperar {Date}; se reintentará.", date);
            }
        }

        var remaining = BackfillPlanner.CountMissing(from, to, existing) - written.Count - present.Count;
        logger.LogInformation(
            "Recuperación histórica {From}..{To}: {Written} escritas, {Present} ya existentes, {Failed} con error, {Remaining} pendientes, {WithoutData} días sin datos de origen.",
            from,
            to,
            written.Count,
            present.Count,
            failed.Count,
            remaining,
            withoutData);

        return new BackfillResult(written, present, failed, withoutData, remaining, false);
    }
}
