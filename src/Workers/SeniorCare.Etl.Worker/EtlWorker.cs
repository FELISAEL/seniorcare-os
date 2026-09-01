using Npgsql;

namespace SeniorCare.Etl.Worker;

public sealed class EtlWorker(
    IConfiguration configuration,
    ILogger<EtlWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuredInterval = configuration.GetValue<int?>("Etl:IntervalSeconds");
        if (!configuredInterval.HasValue
            && int.TryParse(
                configuration["ETL_INTERVAL_SECONDS"],
                out var environmentInterval))
        {
            configuredInterval = environmentInterval;
        }

        var interval = TimeSpan.FromSeconds(
            Math.Clamp(configuredInterval ?? 30, 10, 3600));

        logger.LogInformation(
            "SeniorCare ETL iniciado. Intervalo: {IntervalSeconds} segundos.",
            interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falló una ejecución del proceso ETL.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var careConnection = RequiredConnectionString("CareDatabase");
        var emergencyConnection = RequiredConnectionString("EmergencyDatabase");
        var analyticsConnection = RequiredConnectionString("AnalyticsDatabase");
        var date = DateOnly.FromDateTime(DateTime.Now);

        var (scheduledDoses, takenDoses) = await ExtractMedicationMetricsAsync(
            careConnection,
            date,
            cancellationToken);
        var (assistanceAlerts, emergencyAlerts, resolvedAlerts) =
            await ExtractAlertMetricsAsync(
                emergencyConnection,
                date,
                cancellationToken);

        await LoadAsync(
            analyticsConnection,
            date,
            scheduledDoses,
            takenDoses,
            assistanceAlerts,
            emergencyAlerts,
            resolvedAlerts,
            cancellationToken);

        logger.LogInformation(
            "ETL completado para {Date}: {Taken}/{Scheduled} tomas y {Alerts} alertas.",
            date,
            takenDoses,
            scheduledDoses,
            assistanceAlerts + emergencyAlerts);
    }

    private static async Task<(int Scheduled, int Taken)> ExtractMedicationMetricsAsync(
        string connectionString,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                (
                    SELECT COUNT(*)
                    FROM medication_schedules s
                    INNER JOIN medications m ON m.id = s.medication_id
                    WHERE s.is_daily = true AND m.is_active = true
                )::integer AS scheduled,
                (
                    SELECT COUNT(*)
                    FROM dose_events
                    WHERE scheduled_date = $1 AND status = 'taken'
                )::integer AS taken;
            """;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<(int Assistance, int Emergency, int Resolved)>
        ExtractAlertMetricsAsync(
            string connectionString,
            DateOnly date,
            CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                COUNT(*) FILTER (WHERE type = 'assistance')::integer,
                COUNT(*) FILTER (WHERE type = 'emergency')::integer,
                COUNT(*) FILTER (WHERE status = 'resolved')::integer
            FROM alerts
            WHERE created_at::date = $1;
            """;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static async Task LoadAsync(
        string connectionString,
        DateOnly date,
        int scheduledDoses,
        int takenDoses,
        int assistanceAlerts,
        int emergencyAlerts,
        int resolvedAlerts,
        CancellationToken cancellationToken)
    {
        const string schema =
            """
            CREATE TABLE IF NOT EXISTS daily_metrics (
                metric_date date PRIMARY KEY,
                scheduled_doses integer NOT NULL,
                taken_doses integer NOT NULL,
                assistance_alerts integer NOT NULL,
                emergency_alerts integer NOT NULL,
                resolved_alerts integer NOT NULL,
                etl_run_at timestamptz NOT NULL
            );
            """;

        const string upsert =
            """
            INSERT INTO daily_metrics (
                metric_date, scheduled_doses, taken_doses,
                assistance_alerts, emergency_alerts, resolved_alerts, etl_run_at
            )
            VALUES ($1, $2, $3, $4, $5, $6, now())
            ON CONFLICT (metric_date)
            DO UPDATE SET
                scheduled_doses = EXCLUDED.scheduled_doses,
                taken_doses = EXCLUDED.taken_doses,
                assistance_alerts = EXCLUDED.assistance_alerts,
                emergency_alerts = EXCLUDED.emergency_alerts,
                resolved_alerts = EXCLUDED.resolved_alerts,
                etl_run_at = now();
            """;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using (var schemaCommand = dataSource.CreateCommand(schema))
        {
            await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = dataSource.CreateCommand(upsert);
        command.Parameters.AddWithValue(date);
        command.Parameters.AddWithValue(scheduledDoses);
        command.Parameters.AddWithValue(takenDoses);
        command.Parameters.AddWithValue(assistanceAlerts);
        command.Parameters.AddWithValue(emergencyAlerts);
        command.Parameters.AddWithValue(resolvedAlerts);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string RequiredConnectionString(string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException(
            $"No se configuró ConnectionStrings:{name}.");
}
