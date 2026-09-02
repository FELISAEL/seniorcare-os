using Npgsql;
using SeniorCare.Api.App.Models.Emergency;

namespace SeniorCare.Api.App.Repositories.Emergency;

public sealed class EmergencyRepository : IEmergencyRepository, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public EmergencyRepository(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EmergencyDatabase")
            ?? throw new InvalidOperationException(
                "No se configuró ConnectionStrings:EmergencyDatabase.");
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            CREATE TABLE IF NOT EXISTS alerts (
                id uuid PRIMARY KEY,
                resident_id uuid NOT NULL,
                resident_name varchar(160) NOT NULL,
                type varchar(30) NOT NULL,
                message varchar(500) NOT NULL,
                status varchar(30) NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz,
                updated_by varchar(80)
            );

            CREATE INDEX IF NOT EXISTS ix_alerts_status_created
                ON alerts (status, created_at DESC);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Alert> CreateAsync(
        CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var alert = new Alert(
            Guid.NewGuid(),
            request.ResidentId,
            request.ResidentName.Trim(),
            request.Type.Trim().ToLowerInvariant(),
            request.Message.Trim(),
            "active",
            DateTimeOffset.UtcNow,
            null,
            null);

        const string sql =
            """
            INSERT INTO alerts (
                id, resident_id, resident_name, type, message,
                status, created_at, updated_at, updated_by
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, NULL, NULL);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(alert.Id);
        command.Parameters.AddWithValue(alert.ResidentId);
        command.Parameters.AddWithValue(alert.ResidentName);
        command.Parameters.AddWithValue(alert.Type);
        command.Parameters.AddWithValue(alert.Message);
        command.Parameters.AddWithValue(alert.Status);
        command.Parameters.AddWithValue(alert.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return alert;
    }

    public async Task<IReadOnlyList<Alert>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id, resident_id, resident_name, type, message,
                   status, created_at, updated_at, updated_by
            FROM alerts
            WHERE status IN ('active', 'acknowledged')
            ORDER BY
                CASE WHEN type = 'emergency' THEN 0 ELSE 1 END,
                created_at DESC;
            """;

        return await ListWithCommandAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> ListForResidentAsync(
        Guid residentId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id, resident_id, resident_name, type, message,
                   status, created_at, updated_at, updated_by
            FROM alerts
            WHERE resident_id = $1
            ORDER BY created_at DESC
            LIMIT 30;
            """;

        return await ListWithCommandAsync(sql, residentId, cancellationToken);
    }

    public async Task<Alert?> UpdateStatusAsync(
        Guid alertId,
        string status,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            UPDATE alerts
            SET status = $2,
                updated_at = now(),
                updated_by = $3
            WHERE id = $1
            RETURNING id, resident_id, resident_name, type, message,
                      status, created_at, updated_at, updated_by;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(alertId);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(updatedBy);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadAlert(reader)
            : null;
    }

    private async Task<IReadOnlyList<Alert>> ListWithCommandAsync(
        string sql,
        Guid? residentId,
        CancellationToken cancellationToken)
    {
        var alerts = new List<Alert>();
        await using var command = _dataSource.CreateCommand(sql);
        if (residentId.HasValue)
        {
            command.Parameters.AddWithValue(residentId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            alerts.Add(ReadAlert(reader));
        }

        return alerts;
    }

    private static Alert ReadAlert(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8)
                ? null
                : reader.GetString(8));

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

