using Npgsql;
using NpgsqlTypes;
using SeniorCare.Care.Api.Domain;

namespace SeniorCare.Care.Api.Repositories;

public sealed class CareRepository : ICareRepository, IAsyncDisposable
{
    public static readonly Guid DemoResidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly NpgsqlDataSource _dataSource;

    public CareRepository(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CareDatabase")
            ?? throw new InvalidOperationException(
                "No se configuró ConnectionStrings:CareDatabase.");
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string schema =
            """
            CREATE TABLE IF NOT EXISTS residents (
                id uuid PRIMARY KEY,
                username varchar(80) NOT NULL UNIQUE,
                full_name varchar(160) NOT NULL,
                birth_date date,
                emergency_contact_name varchar(160) NOT NULL,
                emergency_contact_phone varchar(40) NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS medications (
                id uuid PRIMARY KEY,
                resident_id uuid NOT NULL REFERENCES residents(id),
                name varchar(160) NOT NULL,
                dosage varchar(120) NOT NULL,
                instructions varchar(400) NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS medication_schedules (
                id uuid PRIMARY KEY,
                medication_id uuid NOT NULL REFERENCES medications(id) ON DELETE CASCADE,
                time_local time NOT NULL,
                is_daily boolean NOT NULL DEFAULT true
            );

            CREATE TABLE IF NOT EXISTS dose_events (
                id uuid PRIMARY KEY,
                schedule_id uuid NOT NULL REFERENCES medication_schedules(id),
                resident_id uuid NOT NULL REFERENCES residents(id),
                scheduled_date date NOT NULL,
                status varchar(30) NOT NULL,
                confirmed_at timestamptz NOT NULL,
                UNIQUE (schedule_id, scheduled_date)
            );
            """;

        await using (var command = _dataSource.CreateCommand(schema))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SeedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Resident>> ListResidentsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id, username, full_name, birth_date,
                   emergency_contact_name, emergency_contact_phone, created_at
            FROM residents
            ORDER BY full_name;
            """;

        var residents = new List<Resident>();
        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            residents.Add(ReadResident(reader));
        }

        return residents;
    }

    public async Task<Resident?> FindResidentByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id, username, full_name, birth_date,
                   emergency_contact_name, emergency_contact_phone, created_at
            FROM residents
            WHERE lower(username) = lower($1)
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(username.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadResident(reader)
            : null;
    }

    public async Task<Resident?> FindResidentByIdAsync(
        Guid residentId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id, username, full_name, birth_date,
                   emergency_contact_name, emergency_contact_phone, created_at
            FROM residents
            WHERE id = $1
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(residentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadResident(reader)
            : null;
    }

    public async Task<Resident> CreateResidentAsync(
        CreateResidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var resident = new Resident(
            Guid.NewGuid(),
            request.Username.Trim().ToLowerInvariant(),
            request.FullName.Trim(),
            request.BirthDate,
            request.EmergencyContactName.Trim(),
            request.EmergencyContactPhone.Trim(),
            DateTimeOffset.UtcNow);

        const string sql =
            """
            INSERT INTO residents (
                id, username, full_name, birth_date,
                emergency_contact_name, emergency_contact_phone, created_at
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(resident.Id);
        command.Parameters.AddWithValue(resident.Username);
        command.Parameters.AddWithValue(resident.FullName);
        var birthDateParameter = command.Parameters.Add(
            "birth_date",
            NpgsqlDbType.Date);
        birthDateParameter.Value = resident.BirthDate is null
            ? DBNull.Value
            : resident.BirthDate.Value;
        command.Parameters.AddWithValue(resident.EmergencyContactName);
        command.Parameters.AddWithValue(resident.EmergencyContactPhone);
        command.Parameters.AddWithValue(resident.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return resident;
    }

    public async Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
        Guid residentId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT s.id,
                   m.id,
                   m.name,
                   m.dosage,
                   m.instructions,
                   s.time_local,
                   COALESCE(d.status, 'pending') AS status,
                   d.confirmed_at
            FROM medications m
            INNER JOIN medication_schedules s ON s.medication_id = m.id
            LEFT JOIN dose_events d
                ON d.schedule_id = s.id
               AND d.scheduled_date = $2
            WHERE m.resident_id = $1
              AND m.is_active = true
              AND s.is_daily = true
            ORDER BY s.time_local;
            """;

        var schedules = new List<MedicationScheduleView>();
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(residentId);
        command.Parameters.AddWithValue(localDate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            schedules.Add(new MedicationScheduleView(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<TimeOnly>(5),
                reader.GetString(6),
                reader.IsDBNull(7)
                    ? null
                    : reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return schedules;
    }

    public async Task<MedicationScheduleView> CreateMedicationAsync(
        Guid residentId,
        CreateMedicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var medicationId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string medicationSql =
            """
            INSERT INTO medications (
                id, resident_id, name, dosage, instructions, is_active, created_at
            )
            VALUES ($1, $2, $3, $4, $5, true, now());
            """;

        await using (var medication = new NpgsqlCommand(
            medicationSql,
            connection,
            transaction))
        {
            medication.Parameters.AddWithValue(medicationId);
            medication.Parameters.AddWithValue(residentId);
            medication.Parameters.AddWithValue(request.Name.Trim());
            medication.Parameters.AddWithValue(request.Dosage.Trim());
            medication.Parameters.AddWithValue(request.Instructions.Trim());
            await medication.ExecuteNonQueryAsync(cancellationToken);
        }

        const string scheduleSql =
            """
            INSERT INTO medication_schedules (
                id, medication_id, time_local, is_daily
            )
            VALUES ($1, $2, $3, true);
            """;

        await using (var schedule = new NpgsqlCommand(
            scheduleSql,
            connection,
            transaction))
        {
            schedule.Parameters.AddWithValue(scheduleId);
            schedule.Parameters.AddWithValue(medicationId);
            schedule.Parameters.AddWithValue(request.Time);
            await schedule.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new MedicationScheduleView(
            scheduleId,
            medicationId,
            request.Name.Trim(),
            request.Dosage.Trim(),
            request.Instructions.Trim(),
            request.Time,
            "pending",
            null);
    }

    public async Task<DoseConfirmation?> ConfirmDoseAsync(
        Guid residentId,
        Guid scheduleId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            INSERT INTO dose_events (
                id, schedule_id, resident_id, scheduled_date, status, confirmed_at
            )
            SELECT $1, s.id, m.resident_id, $4, 'taken', now()
            FROM medication_schedules s
            INNER JOIN medications m ON m.id = s.medication_id
            WHERE s.id = $2
              AND m.resident_id = $3
            ON CONFLICT (schedule_id, scheduled_date)
            DO UPDATE SET status = 'taken', confirmed_at = now()
            RETURNING schedule_id, resident_id, scheduled_date, status, confirmed_at;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(scheduleId);
        command.Parameters.AddWithValue(residentId);
        command.Parameters.AddWithValue(localDate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new DoseConfirmation(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetFieldValue<DateOnly>(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        const string residentSql =
            """
            INSERT INTO residents (
                id, username, full_name, birth_date,
                emergency_contact_name, emergency_contact_phone, created_at
            )
            VALUES (
                '11111111-1111-1111-1111-111111111111',
                'maria',
                'María López',
                DATE '1948-04-12',
                'Ana López',
                '+502 5555-0101',
                now()
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        const string medicationSql =
            """
            INSERT INTO medications (
                id, resident_id, name, dosage, instructions, is_active, created_at
            )
            VALUES
                (
                    '21111111-1111-1111-1111-111111111111',
                    '11111111-1111-1111-1111-111111111111',
                    'Losartán',
                    '50 mg',
                    'Tomar con un vaso de agua.',
                    true,
                    now()
                ),
                (
                    '31111111-1111-1111-1111-111111111111',
                    '11111111-1111-1111-1111-111111111111',
                    'Vitamina D',
                    '1 tableta',
                    'Tomar después del desayuno.',
                    true,
                    now()
                )
            ON CONFLICT (id) DO NOTHING;
            """;

        const string scheduleSql =
            """
            INSERT INTO medication_schedules (
                id, medication_id, time_local, is_daily
            )
            VALUES
                (
                    '41111111-1111-1111-1111-111111111111',
                    '21111111-1111-1111-1111-111111111111',
                    TIME '12:00',
                    true
                ),
                (
                    '51111111-1111-1111-1111-111111111111',
                    '31111111-1111-1111-1111-111111111111',
                    TIME '08:00',
                    true
                )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using (var resident = _dataSource.CreateCommand(residentSql))
        {
            await resident.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var medication = _dataSource.CreateCommand(medicationSql))
        {
            await medication.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var schedule = _dataSource.CreateCommand(scheduleSql);
        await schedule.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Resident ReadResident(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3)
                ? null
                : reader.GetFieldValue<DateOnly>(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6));

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
