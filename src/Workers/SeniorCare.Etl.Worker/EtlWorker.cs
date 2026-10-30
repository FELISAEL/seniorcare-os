namespace SeniorCare.Etl.Worker;

public sealed class EtlWorker(
    IMetricsStore store,
    MetricsProcessor processor,
    EtlOptions options,
    ILogger<EtlWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SeniorCare ETL iniciado. Intervalo: {IntervalSeconds} segundos. Zona: {TimeZone}. Recuperación histórica: {Backfill}.",
            options.Interval.TotalSeconds,
            options.TimeZone.Id,
            options.BackfillEnabled
                ? $"habilitada ({options.BackfillDays} días, lotes de {options.BackfillBatchDays})"
                : "deshabilitada");

        var schemaReady = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // El esquema se ajusta una vez por arranque (y se reintenta si falla).
                if (!schemaReady)
                {
                    await store.EnsureSchemaAsync(stoppingToken);
                    schemaReady = true;
                }

                await processor.RunCycleAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Falló una ejecución del proceso ETL.");
            }

            await Task.Delay(options.Interval, stoppingToken);
        }
    }
}
