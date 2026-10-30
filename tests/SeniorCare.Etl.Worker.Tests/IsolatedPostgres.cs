using Npgsql;
using SeniorCare.Etl.Worker;

namespace SeniorCare.Etl.Worker.Tests;

/// <summary>
/// Prueba que necesita un PostgreSQL aislado. Se omite si SENIORCARE_ETL_TEST_POSTGRES no está
/// definida, para que `dotnet test` nunca apunte por accidente a una base real.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(IsolatedPostgres.AdminConnectionString))
        {
            Skip = $"Defina {IsolatedPostgres.EnvironmentVariable} con un PostgreSQL aislado para ejecutarla.";
        }
    }
}

/// <summary>
/// Crea tres bases temporales (care, emergency, analytics) con nombre único en un servidor aislado
/// y las elimina al terminar. Se niega a trabajar en un servidor con las bases de SeniorCare.
/// </summary>
public sealed class IsolatedPostgres : IAsyncDisposable
{
    public const string EnvironmentVariable = "SENIORCARE_ETL_TEST_POSTGRES";

    public static string? AdminConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable);

    private static readonly string[] ProductionDatabases =
        ["care_db", "emergency_db", "analytics_db", "identity_db"];

    private readonly string _suffix = Guid.NewGuid().ToString("N")[..10];

    public string CareConnection { get; private set; } = "";
    public string EmergencyConnection { get; private set; } = "";
    public string AnalyticsConnection { get; private set; } = "";

    public static async Task<IsolatedPostgres> CreateAsync()
    {
        var database = new IsolatedPostgres();
        await database.InitializeAsync();
        return database;
    }

    public PostgresMetricsStore CreateStore(EtlOptions options) =>
        new(CareConnection, EmergencyConnection, AnalyticsConnection, options);

    public async Task ExecuteAsync(string connection, string sql, params object[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(connection);
        await using var command = dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter is DateTimeOffset instant ? instant.ToUniversalTime() : parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string connection, string sql, params object[] parameters)
    {
        await using var dataSource = NpgsqlDataSource.Create(connection);
        await using var command = dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter is DateTimeOffset instant ? instant.ToUniversalTime() : parameter);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }

    // ---------- Datos de origen mínimos (mismas columnas que usan la API y el ETL) ----------

    public Task AddResidentAsync(Guid id, DateTimeOffset createdAt) =>
        ExecuteAsync(
            CareConnection,
            "INSERT INTO residents (id, full_name, created_at) VALUES ($1, 'Residente de prueba', $2);",
            id,
            createdAt);

    public async Task AddDailyMedicationAsync(Guid residentId, DateTimeOffset createdAt, bool isActive = true)
    {
        var medicationId = Guid.NewGuid();
        await ExecuteAsync(
            CareConnection,
            "INSERT INTO medications (id, resident_id, is_active, created_at) VALUES ($1, $2, $3, $4);",
            medicationId,
            residentId,
            isActive,
            createdAt);
        await ExecuteAsync(
            CareConnection,
            "INSERT INTO medication_schedules (id, medication_id, time_local, is_daily) VALUES ($1, $2, '08:00', true);",
            Guid.NewGuid(),
            medicationId);
    }

    public Task AddTakenDoseAsync(Guid residentId, DateOnly date) =>
        ExecuteAsync(
            CareConnection,
            "INSERT INTO dose_events (id, schedule_id, resident_id, scheduled_date, status, confirmed_at) VALUES ($1, $2, $3, $4, 'taken', now());",
            Guid.NewGuid(),
            Guid.NewGuid(),
            residentId,
            date);

    public Task AddAlertAsync(Guid residentId, string type, string status, DateTimeOffset createdAt) =>
        ExecuteAsync(
            EmergencyConnection,
            "INSERT INTO alerts (id, resident_id, type, status, created_at) VALUES ($1, $2, $3, $4, $5);",
            Guid.NewGuid(),
            residentId,
            type,
            status,
            createdAt);

    private async Task InitializeAsync()
    {
        var admin = AdminConnectionString
            ?? throw new InvalidOperationException($"Falta {EnvironmentVariable}.");

        await using var dataSource = NpgsqlDataSource.Create(admin);
        await using (var check = dataSource.CreateCommand(
            "SELECT count(*) FROM pg_database WHERE datname = ANY($1);"))
        {
            check.Parameters.AddWithValue(ProductionDatabases);
            if ((long)(await check.ExecuteScalarAsync())! > 0)
            {
                throw new InvalidOperationException(
                    "El servidor de pruebas contiene bases de SeniorCare: use una instancia aislada.");
            }
        }

        CareConnection = await CreateDatabaseAsync(dataSource, admin, "care");
        EmergencyConnection = await CreateDatabaseAsync(dataSource, admin, "emergency");
        AnalyticsConnection = await CreateDatabaseAsync(dataSource, admin, "analytics");

        await ExecuteAsync(
            CareConnection,
            """
            CREATE TABLE residents (id uuid PRIMARY KEY, full_name varchar(160) NOT NULL, created_at timestamptz NOT NULL);
            CREATE TABLE medications (id uuid PRIMARY KEY, resident_id uuid NOT NULL, is_active boolean NOT NULL, created_at timestamptz NOT NULL);
            CREATE TABLE medication_schedules (id uuid PRIMARY KEY, medication_id uuid NOT NULL, time_local time NOT NULL, is_daily boolean NOT NULL);
            CREATE TABLE dose_events (
                id uuid PRIMARY KEY, schedule_id uuid NOT NULL, resident_id uuid NOT NULL,
                scheduled_date date NOT NULL, status varchar(20) NOT NULL, confirmed_at timestamptz NOT NULL);
            """);
        await ExecuteAsync(
            EmergencyConnection,
            """
            CREATE TABLE alerts (
                id uuid PRIMARY KEY, resident_id uuid NOT NULL, type varchar(20) NOT NULL,
                status varchar(20) NOT NULL, created_at timestamptz NOT NULL);
            """);
    }

    private async Task<string> CreateDatabaseAsync(NpgsqlDataSource dataSource, string admin, string name)
    {
        var database = $"etl_test_{name}_{_suffix}";
        await using (var command = dataSource.CreateCommand($"CREATE DATABASE \"{database}\";"))
        {
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(admin) { Database = database, Pooling = false }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (AdminConnectionString is not { } admin)
        {
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(admin);
        foreach (var name in new[] { "care", "emergency", "analytics" })
        {
            await using var command = dataSource.CreateCommand(
                $"DROP DATABASE IF EXISTS \"etl_test_{name}_{_suffix}\" WITH (FORCE);");
            await command.ExecuteNonQueryAsync();
        }
    }
}
