using Npgsql;
using NpgsqlTypes;
using SeniorCare.Api.App.Models.Emergency;

namespace SeniorCare.Api.App.Repositories.Emergency;

public sealed class EmergencyRepository : IEmergencyRepository, IAsyncDisposable
{
    private const string Columns =
        "id, resident_id, resident_name, type, message, status, created_at, "
        + "updated_at, updated_by, received_at, client_request_id";

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

            -- Etapa 11.1 (solo agrega columnas, no toca filas existentes):
            -- client_request_id: identificador del dispositivo para no duplicar reintentos.
            -- received_at: llegada al servidor; created_at pasa a ser la hora real del evento.
            -- reported_at: hora tal como la envió el dispositivo, sin corregir (auditoría).
            ALTER TABLE alerts ADD COLUMN IF NOT EXISTS client_request_id uuid NULL;
            ALTER TABLE alerts ADD COLUMN IF NOT EXISTS received_at timestamptz NULL;
            ALTER TABLE alerts ADD COLUMN IF NOT EXISTS reported_at timestamptz NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_alerts_client_request_id
                ON alerts (client_request_id)
                WHERE client_request_id IS NOT NULL;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AlertCreationResult> CreateAsync(
        Alert alert,
        DateTimeOffset? reportedAt,
        CancellationToken cancellationToken = default)
    {
        const string insertSql =
            $"""
            INSERT INTO alerts (
                id, resident_id, resident_name, type, message,
                status, created_at, updated_at, updated_by,
                received_at, client_request_id, reported_at
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, NULL, NULL, $8, $9, $10)
            ON CONFLICT (client_request_id) WHERE client_request_id IS NOT NULL
            DO NOTHING
            RETURNING {Columns};
            """;

        await using (var command = _dataSource.CreateCommand(insertSql))
        {
            command.Parameters.AddWithValue(alert.Id);
            command.Parameters.AddWithValue(alert.ResidentId);
            command.Parameters.AddWithValue(alert.ResidentName);
            command.Parameters.AddWithValue(alert.Type);
            command.Parameters.AddWithValue(alert.Message);
            command.Parameters.AddWithValue(alert.Status);
            // Npgsql solo admite instantes UTC en columnas timestamptz.
            command.Parameters.AddWithValue(alert.CreatedAt.ToUniversalTime());
            command.Parameters.AddWithValue((alert.ReceivedAt ?? alert.CreatedAt).ToUniversalTime());
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = (object?)alert.ClientRequestId ?? DBNull.Value,
                NpgsqlDbType = NpgsqlDbType.Uuid
            });
            command.Parameters.Add(new NpgsqlParameter
            {
                Value = (object?)reportedAt?.ToUniversalTime() ?? DBNull.Value,
                NpgsqlDbType = NpgsqlDbType.TimestampTz
            });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new AlertCreationResult(ReadAlert(reader), true);
            }
        }

        // Reintento de un aviso ya registrado: se devuelve el original sin crear otro.
        const string existingSql =
            $"""
            SELECT {Columns}
            FROM alerts
            WHERE client_request_id = $1;
            """;

        await using var existing = _dataSource.CreateCommand(existingSql);
        existing.Parameters.AddWithValue(alert.ClientRequestId!.Value);
        await using var existingReader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await existingReader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "No se pudo registrar ni recuperar el aviso con ese identificador.");
        }

        return new AlertCreationResult(ReadAlert(existingReader), false);
    }

    public async Task<IReadOnlyList<Alert>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            $"""
            SELECT {Columns}
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
            $"""
            SELECT {Columns}
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
            $"""
            UPDATE alerts
            SET status = $2,
                updated_at = now(),
                updated_by = $3
            WHERE id = $1
            RETURNING {Columns};
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
                : reader.GetString(8),
            reader.IsDBNull(9)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10)
                ? null
                : reader.GetGuid(10));

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

