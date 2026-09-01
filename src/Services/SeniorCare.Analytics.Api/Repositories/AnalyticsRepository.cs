using Npgsql;
using SeniorCare.Analytics.Api.Domain;

namespace SeniorCare.Analytics.Api.Repositories;

public sealed class AnalyticsRepository : IAnalyticsRepository, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public AnalyticsRepository(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AnalyticsDatabase")
            ?? throw new InvalidOperationException(
                "No se configuró ConnectionStrings:AnalyticsDatabase.");
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
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
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DailyMetric?> FindAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT metric_date, scheduled_doses, taken_doses,
                   assistance_alerts, emergency_alerts, resolved_alerts, etl_run_at
            FROM daily_metrics
            WHERE metric_date = $1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(date);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMetric(reader)
            : null;
    }

    public async Task<IReadOnlyList<DailyMetric>> ListRecentAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT metric_date, scheduled_doses, taken_doses,
                   assistance_alerts, emergency_alerts, resolved_alerts, etl_run_at
            FROM daily_metrics
            ORDER BY metric_date DESC
            LIMIT $1;
            """;

        var metrics = new List<DailyMetric>();
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(days);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            metrics.Add(ReadMetric(reader));
        }

        return metrics;
    }

    private static DailyMetric ReadMetric(NpgsqlDataReader reader) =>
        new(
            reader.GetFieldValue<DateOnly>(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetFieldValue<DateTimeOffset>(6));

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

