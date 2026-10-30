using Npgsql;

namespace SeniorCare.Etl.Worker;

/// <summary>
/// Lee care_db y emergency_db (solo SELECT) y escribe únicamente analytics_db.daily_metrics.
/// Las fechas de las alertas se calculan con la zona configurada, no con la de la sesión.
/// </summary>
public sealed class PostgresMetricsStore : IMetricsStore, IAsyncDisposable
{
    // Clave del bloqueo consultivo que impide dos recuperaciones históricas simultáneas.
    private const string BackfillLockName = "seniorcare:etl:backfill";

    private readonly NpgsqlDataSource _care;
    private readonly NpgsqlDataSource _emergency;
    private readonly NpgsqlDataSource _analytics;
    private readonly string _timeZoneId;

    public PostgresMetricsStore(
        string careConnectionString,
        string emergencyConnectionString,
        string analyticsConnectionString,
        EtlOptions options)
    {
        _care = NpgsqlDataSource.Create(careConnectionString);
        _emergency = NpgsqlDataSource.Create(emergencyConnectionString);
        _analytics = NpgsqlDataSource.Create(analyticsConnectionString);
        _timeZoneId = options.TimeZone.Id;
    }

    public static PostgresMetricsStore FromConfiguration(IConfiguration configuration, EtlOptions options) =>
        new(
            Required(configuration, "CareDatabase"),
            Required(configuration, "EmergencyDatabase"),
            Required(configuration, "AnalyticsDatabase"),
            options);

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        // Cambios aditivos e idempotentes: ninguna fila se borra ni se reescribe.
        const string sql =
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
            ALTER TABLE daily_metrics ALTER COLUMN scheduled_doses DROP NOT NULL;
            ALTER TABLE daily_metrics
                ADD COLUMN IF NOT EXISTS data_quality varchar(16) NOT NULL DEFAULT 'complete';
            """;

        await using var command = _analytics.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountCurrentScheduledDosesAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT COUNT(*)::integer
            FROM medication_schedules s
            INNER JOIN medications m ON m.id = s.medication_id
            WHERE s.is_daily = true AND m.is_active = true;
            """;

        await using var command = _care.CreateCommand(sql);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<DailyFacts> ExtractFactsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        const string dosesSql =
            """
            SELECT COUNT(*)::integer
            FROM dose_events
            WHERE scheduled_date = $1 AND status = 'taken';
            """;

        // Alertas creadas en esa fecha local. resolved_alerts refleja el estado al momento del cálculo.
        const string alertsSql =
            """
            SELECT
                COUNT(*) FILTER (WHERE type = 'assistance')::integer,
                COUNT(*) FILTER (WHERE type = 'emergency')::integer,
                COUNT(*) FILTER (WHERE status = 'resolved')::integer
            FROM alerts
            WHERE (created_at AT TIME ZONE $2)::date = $1;
            """;

        int taken;
        await using (var command = _care.CreateCommand(dosesSql))
        {
            command.Parameters.AddWithValue(date);
            taken = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        await using var alerts = _emergency.CreateCommand(alertsSql);
        alerts.Parameters.AddWithValue(date);
        alerts.Parameters.AddWithValue(_timeZoneId);
        await using var reader = await alerts.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DailyFacts(taken, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    public async Task UpsertCompleteAsync(
        DateOnly date,
        int scheduledDoses,
        DailyFacts facts,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO daily_metrics (
                metric_date, scheduled_doses, taken_doses,
                assistance_alerts, emergency_alerts, resolved_alerts, etl_run_at, data_quality
            )
            VALUES ($1, $2, $3, $4, $5, $6, now(), 'complete')
            ON CONFLICT (metric_date)
            DO UPDATE SET
                scheduled_doses = EXCLUDED.scheduled_doses,
                taken_doses = EXCLUDED.taken_doses,
                assistance_alerts = EXCLUDED.assistance_alerts,
                emergency_alerts = EXCLUDED.emergency_alerts,
                resolved_alerts = EXCLUDED.resolved_alerts,
                etl_run_at = now(),
                data_quality = 'complete';
            """;

        await using var command = _analytics.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        command.Parameters.AddWithValue(scheduledDoses);
        AddFacts(command, facts);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> NeedsClosingAsync(
        DateOnly date,
        DateTimeOffset dayEnd,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT EXISTS (
                SELECT 1 FROM daily_metrics
                WHERE metric_date = $1 AND data_quality = 'complete' AND etl_run_at < $2
            );
            """;

        await using var command = _analytics.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        // Npgsql solo admite instantes UTC en columnas timestamptz.
        command.Parameters.AddWithValue(dayEnd.ToUniversalTime());
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> CloseCompleteDayAsync(
        DateOnly date,
        DateTimeOffset dayEnd,
        DailyFacts facts,
        CancellationToken cancellationToken)
    {
        // scheduled_doses no se toca: es el plan registrado durante ese día.
        const string sql =
            """
            UPDATE daily_metrics SET
                taken_doses = $2,
                assistance_alerts = $3,
                emergency_alerts = $4,
                resolved_alerts = $5,
                etl_run_at = now()
            WHERE metric_date = $1 AND data_quality = 'complete' AND etl_run_at < $6;
            """;

        await using var command = _analytics.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        AddFacts(command, facts);
        // Npgsql solo admite instantes UTC en columnas timestamptz.
        command.Parameters.AddWithValue(dayEnd.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<DateOnly?> FindDataStartAsync(CancellationToken cancellationToken)
    {
        const string careSql =
            """
            SELECT LEAST(
                (SELECT MIN((created_at AT TIME ZONE $1)::date) FROM residents),
                (SELECT MIN(scheduled_date) FROM dose_events)
            );
            """;
        const string emergencySql =
            "SELECT MIN((created_at AT TIME ZONE $1)::date) FROM alerts;";

        var care = await ScalarDateAsync(_care, careSql, cancellationToken);
        var emergency = await ScalarDateAsync(_emergency, emergencySql, cancellationToken);
        return (care, emergency) switch
        {
            (null, null) => null,
            ({ } a, null) => a,
            (null, { } b) => b,
            ({ } a, { } b) => a < b ? a : b
        };
    }

    public async Task<IReadOnlySet<DateOnly>> ListMetricDatesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        const string sql =
            "SELECT metric_date FROM daily_metrics WHERE metric_date BETWEEN $1 AND $2;";

        var dates = new HashSet<DateOnly>();
        await using var command = _analytics.CreateCommand(sql);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dates.Add(reader.GetFieldValue<DateOnly>(0));
        }

        return dates;
    }

    public async Task<bool> InsertPartialIfMissingAsync(
        DateOnly date,
        DailyFacts facts,
        CancellationToken cancellationToken)
    {
        // DO NOTHING: una fecha con métricas (completas o parciales) nunca se reescribe.
        const string sql =
            """
            INSERT INTO daily_metrics (
                metric_date, scheduled_doses, taken_doses,
                assistance_alerts, emergency_alerts, resolved_alerts, etl_run_at, data_quality
            )
            VALUES ($1, NULL, $2, $3, $4, $5, now(), 'partial')
            ON CONFLICT (metric_date) DO NOTHING;
            """;

        await using var command = _analytics.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        AddFacts(command, facts);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IAsyncDisposable?> TryAcquireBackfillLockAsync(CancellationToken cancellationToken)
    {
        var connection = await _analytics.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(hashtext($1));",
            connection);
        command.Parameters.AddWithValue(BackfillLockName);

        if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
        {
            return new AdvisoryLock(connection);
        }

        await connection.DisposeAsync();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _care.DisposeAsync();
        await _emergency.DisposeAsync();
        await _analytics.DisposeAsync();
    }

    private static void AddFacts(NpgsqlCommand command, DailyFacts facts)
    {
        command.Parameters.AddWithValue(facts.TakenDoses);
        command.Parameters.AddWithValue(facts.AssistanceAlerts);
        command.Parameters.AddWithValue(facts.EmergencyAlerts);
        command.Parameters.AddWithValue(facts.ResolvedAlerts);
    }

    private async Task<DateOnly?> ScalarDateAsync(
        NpgsqlDataSource dataSource,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(_timeZoneId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateOnly date ? date : value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : null;
    }

    private static string Required(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"No se configuró ConnectionStrings:{name}.");

    // El bloqueo consultivo vive en la conexión: se libera al soltarlo o al cerrarla.
    private sealed class AdvisoryLock(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(hashtext($1));",
                    connection);
                command.Parameters.AddWithValue(BackfillLockName);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
