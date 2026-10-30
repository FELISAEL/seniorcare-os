using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SeniorCare.Etl.Worker;

namespace SeniorCare.Etl.Worker.Tests;

/// <summary>
/// Recuperación histórica contra un PostgreSQL aislado (ver IsolatedPostgres).
/// Escenario base: el sistema empieza el 28 de octubre y hoy es 30 de octubre, 10:00 en Guatemala.
/// </summary>
[Collection("PostgresAislado")]
public sealed class BackfillIntegrationTests
{
    private static readonly Guid Resident = Guid.NewGuid();
    private static readonly DateOnly Oct28 = new(2026, 10, 28);
    private static readonly DateOnly Oct29 = new(2026, 10, 29);
    private static readonly DateOnly Oct30 = new(2026, 10, 30);
    private static readonly TimeSpan Guatemala = TimeSpan.FromHours(-6);

    [PostgresFact]
    public async Task DiaSinMetricas_SeEscribeParcialSinInventarElPlan()
    {
        await using var db = await SeededAsync();
        await db.AddTakenDoseAsync(Resident, Oct29);
        await db.AddAlertAsync(Resident, "emergency", "active", Local(Oct29, 21, 44));

        var (processor, store) = Create(db);
        await using var _ = store;
        var result = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal([Oct28, Oct29], result.Written);
        var row = await RowAsync(db, Oct29);
        Assert.Equal("NULL|1|0|1|0|partial", row);
    }

    [PostgresFact]
    public async Task TomaConFechaInvalida_NoAdelantaElInicioNiInventaDias()
    {
        // Caso de la auditoría: una confirmación fechada en 2020 (antes de existir el residente)
        // y otra en el futuro no deben extender la recuperación a días sin actividad real.
        await using var db = await SeededAsync();
        await db.AddTakenDoseAsync(Resident, new DateOnly(2020, 1, 1));
        await db.AddTakenDoseAsync(Resident, new DateOnly(2099, 1, 1));

        var (processor, store) = Create(db);
        await using var _ = store;
        Assert.Equal(Oct28, await store.FindDataStartAsync(CancellationToken.None));

        var result = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal([Oct28, Oct29], result.Written);
    }

    [PostgresFact]
    public async Task DiaConMetricasExistentes_NoSeModifica()
    {
        await using var db = await SeededAsync();
        var (processor, store) = Create(db);
        await using var _ = store;
        await store.EnsureSchemaAsync(CancellationToken.None);
        await store.UpsertCompleteAsync(Oct29, 5, new DailyFacts(4, 0, 0, 0), CancellationToken.None);
        var before = await RunAtAsync(db, Oct29);

        var result = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal([Oct28], result.Written);
        Assert.Equal("5|4|0|0|0|complete", await RowAsync(db, Oct29));
        Assert.Equal(before, await RunAtAsync(db, Oct29));
    }

    [PostgresFact]
    public async Task VariosDias_SeRecuperanEnOrdenYPorLotes()
    {
        await using var db = await SeededAsync(dataStart: new DateOnly(2026, 10, 20));
        var (processor, store) = Create(db, batchDays: 4);
        await using var _ = store;

        var first = await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        var second = await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        var third = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal(DatesFrom(new DateOnly(2026, 10, 20), 4), first.Written);
        Assert.Equal(6, first.RemainingMissing);
        Assert.Equal(DatesFrom(new DateOnly(2026, 10, 24), 4), second.Written);
        Assert.Equal(DatesFrom(new DateOnly(2026, 10, 28), 2), third.Written);
        Assert.Equal(0, third.RemainingMissing);
        Assert.Equal(10L, await CountRowsAsync(db));
    }

    [PostgresFact]
    public async Task EjecucionRepetida_NoDuplicaNiAlteraResultados()
    {
        await using var db = await SeededAsync();
        await db.AddAlertAsync(Resident, "assistance", "active", Local(Oct28, 23, 59));
        var (processor, store) = Create(db);
        await using var _ = store;

        await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        var snapshot = await DumpAsync(db);
        var again = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Empty(again.Written);
        Assert.Equal(snapshot, await DumpAsync(db));
        Assert.Equal(2L, await CountRowsAsync(db));
    }

    [PostgresFact]
    public async Task FechasSinDatosDeOrigen_NoSeRellenanConCeros()
    {
        await using var db = await SeededAsync();
        var (processor, store) = Create(db, windowDays: 30);
        await using var _ = store;

        var result = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal(28, result.WithoutSourceData);
        Assert.Equal(0L, await db.ScalarAsync<long>(
            db.AnalyticsConnection,
            "SELECT count(*) FROM daily_metrics WHERE metric_date < $1;",
            Oct28));
    }

    [PostgresFact]
    public async Task SinNingunDatoDeOrigen_NoEscribeNada()
    {
        await using var db = await IsolatedPostgres.CreateAsync();
        var (processor, store) = Create(db);
        await using var _ = store;
        await store.EnsureSchemaAsync(CancellationToken.None);

        var result = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Empty(result.Written);
        Assert.Equal(0L, await CountRowsAsync(db));
    }

    [PostgresFact]
    public async Task AlertaCercaDeMedianoche_CuentaEnSuFechaLocalAunqueElServidorUseUtc()
    {
        await using var db = await SeededAsync();
        // 23:30 del 29 en Guatemala = 05:30 UTC del 30. El servidor de pruebas trabaja en UTC.
        await db.AddAlertAsync(Resident, "assistance", "active", Local(Oct29, 23, 30));
        await db.AddAlertAsync(Resident, "emergency", "active", Local(Oct30, 0, 15));
        var (processor, store) = Create(db);
        await using var _ = store;

        await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        var today = await store.ExtractFactsAsync(Oct30, CancellationToken.None);

        Assert.Equal("NULL|0|1|0|0|partial", await RowAsync(db, Oct29));
        Assert.Equal(new DailyFacts(0, 0, 1, 0), today);
    }

    [PostgresFact]
    public async Task FalloParcial_NoDetieneElLoteYLaFechaSeReintenta()
    {
        await using var db = await SeededAsync();
        await using var realStore = db.CreateStore(Options());
        var failing = new FailingOnceStore(realStore, Oct28);
        var processor = new MetricsProcessor(failing, Options(), Clock(), NullLogger<MetricsProcessor>.Instance);
        await realStore.EnsureSchemaAsync(CancellationToken.None);

        var first = await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        var retry = await processor.RunBackfillAsync(Oct30, CancellationToken.None);

        Assert.Equal([Oct28], first.Failed);
        Assert.Equal([Oct29], first.Written);
        Assert.Equal([Oct28], retry.Written);
        Assert.Empty(retry.Failed);
        Assert.Equal(2L, await CountRowsAsync(db));
    }

    [PostgresFact]
    public async Task DosRecuperacionesSimultaneas_SoloUnaSeEjecuta()
    {
        await using var db = await SeededAsync();
        var (processor, store) = Create(db);
        await using var _ = store;
        await using var other = db.CreateStore(Options());

        await using (var held = await other.TryAcquireBackfillLockAsync(CancellationToken.None))
        {
            Assert.NotNull(held);
            var blocked = await processor.RunBackfillAsync(Oct30, CancellationToken.None);
            Assert.True(blocked.SkippedByLock);
            Assert.Equal(0L, await CountRowsAsync(db));
        }

        var afterRelease = await processor.RunBackfillAsync(Oct30, CancellationToken.None);
        Assert.False(afterRelease.SkippedByLock);
        Assert.Equal(2, afterRelease.Written.Count);
    }

    [PostgresFact]
    public async Task CicloPeriodico_EscribeHoyCompletoYLaRecuperacionNoLoToca()
    {
        await using var db = await SeededAsync();
        await db.AddTakenDoseAsync(Resident, Oct30);
        var (processor, store) = Create(db, backfill: true);
        await using var _ = store;
        await store.EnsureSchemaAsync(CancellationToken.None);

        await processor.RunCycleAsync(CancellationToken.None);
        await processor.RunCycleAsync(CancellationToken.None);

        Assert.Equal("2|1|0|0|0|complete", await RowAsync(db, Oct30));
        Assert.Equal("partial", await QualityAsync(db, Oct29));
        Assert.Equal(3L, await CountRowsAsync(db));
    }

    [PostgresFact]
    public async Task CambioDeDia_CierraAyerConservandoSuPlanUnaSolaVez()
    {
        await using var db = await SeededAsync();
        var (processor, store) = Create(db);
        await using var _ = store;
        await store.EnsureSchemaAsync(CancellationToken.None);
        // Fila del 29 escrita durante ese día (plan de 2 tomas) y antes de una toma de última hora.
        await store.UpsertCompleteAsync(Oct29, 2, new DailyFacts(0, 0, 0, 0), CancellationToken.None);
        await db.ExecuteAsync(db.AnalyticsConnection,
            "UPDATE daily_metrics SET etl_run_at = $2 WHERE metric_date = $1;", Oct29, Local(Oct29, 23, 59));
        await db.AddTakenDoseAsync(Resident, Oct29);
        // Desde entonces el plan cambió: hoy hay 3 tomas diarias. El cierre no debe usar ese plan.
        await db.AddDailyMedicationAsync(Resident, Local(Oct30, 1, 0));

        var closed = await processor.CloseYesterdayAsync(Oct30, CancellationToken.None);
        var closedAgain = await processor.CloseYesterdayAsync(Oct30, CancellationToken.None);

        Assert.True(closed);
        Assert.False(closedAgain);
        Assert.Equal("2|1|0|0|0|complete", await RowAsync(db, Oct29));
    }

    [PostgresFact]
    public async Task EsquemaAnterior_SeAmpliaSinPerderNiReescribirFilas()
    {
        await using var db = await IsolatedPostgres.CreateAsync();
        await db.ExecuteAsync(
            db.AnalyticsConnection,
            """
            CREATE TABLE daily_metrics (
                metric_date date PRIMARY KEY, scheduled_doses integer NOT NULL, taken_doses integer NOT NULL,
                assistance_alerts integer NOT NULL, emergency_alerts integer NOT NULL,
                resolved_alerts integer NOT NULL, etl_run_at timestamptz NOT NULL);
            INSERT INTO daily_metrics VALUES ('2026-10-30', 2, 0, 0, 0, 0, '2026-10-30T06:24:48Z');
            """);
        await using var store = db.CreateStore(Options());

        await store.EnsureSchemaAsync(CancellationToken.None);
        await store.EnsureSchemaAsync(CancellationToken.None);

        Assert.Equal("2|0|0|0|0|complete", await RowAsync(db, Oct30));
        Assert.Equal(
            new DateTimeOffset(2026, 10, 30, 6, 24, 48, TimeSpan.Zero),
            await RunAtAsync(db, Oct30));
    }

    // ---------- Apoyo ----------

    private static async Task<IsolatedPostgres> SeededAsync(DateOnly? dataStart = null)
    {
        var db = await IsolatedPostgres.CreateAsync();
        try
        {
            var start = dataStart ?? Oct28;
            await db.AddResidentAsync(Resident, Local(start, 9, 0));
            await db.AddDailyMedicationAsync(Resident, Local(start, 9, 5));
            await db.AddDailyMedicationAsync(Resident, Local(start, 9, 6));
            return db;
        }
        catch
        {
            // Si la preparación falla, las bases temporales se eliminan igual.
            await db.DisposeAsync();
            throw;
        }
    }

    private static (MetricsProcessor Processor, PostgresMetricsStore Store) Create(
        IsolatedPostgres db,
        int windowDays = 30,
        int batchDays = 7,
        bool backfill = false)
    {
        var options = Options(windowDays, batchDays, backfill);
        var store = db.CreateStore(options);
        store.EnsureSchemaAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (new MetricsProcessor(store, options, Clock(), NullLogger<MetricsProcessor>.Instance), store);
    }

    private static EtlOptions Options(int windowDays = 30, int batchDays = 7, bool backfill = false) =>
        EtlOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ETL_BACKFILL_ENABLED"] = backfill.ToString(),
                ["ETL_BACKFILL_DAYS"] = windowDays.ToString(),
                ["ETL_BACKFILL_BATCH_DAYS"] = batchDays.ToString()
            })
            .Build());

    private static FixedClock Clock() => new(Local(Oct30, 10, 0));

    private static DateTimeOffset Local(DateOnly date, int hour, int minute) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), Guatemala);

    private static IReadOnlyList<DateOnly> DatesFrom(DateOnly start, int count) =>
        Enumerable.Range(0, count).Select(start.AddDays).ToList();

    private static Task<string> RowAsync(IsolatedPostgres db, DateOnly date) =>
        db.ScalarAsync<string>(
            db.AnalyticsConnection,
            """
            SELECT coalesce(scheduled_doses::text, 'NULL') || '|' || taken_doses || '|' || assistance_alerts
                   || '|' || emergency_alerts || '|' || resolved_alerts || '|' || data_quality
            FROM daily_metrics WHERE metric_date = $1;
            """,
            date);

    private static Task<string> QualityAsync(IsolatedPostgres db, DateOnly date) =>
        db.ScalarAsync<string>(db.AnalyticsConnection, "SELECT data_quality FROM daily_metrics WHERE metric_date = $1;", date);

    private static Task<DateTimeOffset> RunAtAsync(IsolatedPostgres db, DateOnly date) =>
        db.ScalarAsync<DateTime>(db.AnalyticsConnection, "SELECT etl_run_at FROM daily_metrics WHERE metric_date = $1;", date)
            .ContinueWith(task => new DateTimeOffset(task.Result, TimeSpan.Zero));

    private static Task<long> CountRowsAsync(IsolatedPostgres db) =>
        db.ScalarAsync<long>(db.AnalyticsConnection, "SELECT count(*) FROM daily_metrics;");

    private static Task<string> DumpAsync(IsolatedPostgres db) =>
        db.ScalarAsync<string>(
            db.AnalyticsConnection,
            "SELECT coalesce(string_agg(t::text, ';' ORDER BY metric_date), '') FROM daily_metrics t;");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    /// <summary>Simula un fallo transitorio al leer una fecha concreta la primera vez.</summary>
    private sealed class FailingOnceStore(IMetricsStore inner, DateOnly failingDate) : IMetricsStore
    {
        private bool _failed;

        public Task<DailyFacts> ExtractFactsAsync(DateOnly date, CancellationToken cancellationToken)
        {
            if (date == failingDate && !_failed)
            {
                _failed = true;
                throw new TimeoutException("Fallo simulado de la base de origen.");
            }

            return inner.ExtractFactsAsync(date, cancellationToken);
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken) => inner.EnsureSchemaAsync(cancellationToken);
        public Task<int> CountCurrentScheduledDosesAsync(CancellationToken cancellationToken) => inner.CountCurrentScheduledDosesAsync(cancellationToken);
        public Task UpsertCompleteAsync(DateOnly date, int scheduledDoses, DailyFacts facts, CancellationToken cancellationToken) => inner.UpsertCompleteAsync(date, scheduledDoses, facts, cancellationToken);
        public Task<bool> CloseCompleteDayAsync(DateOnly date, DateTimeOffset dayEnd, DailyFacts facts, CancellationToken cancellationToken) => inner.CloseCompleteDayAsync(date, dayEnd, facts, cancellationToken);
        public Task<bool> NeedsClosingAsync(DateOnly date, DateTimeOffset dayEnd, CancellationToken cancellationToken) => inner.NeedsClosingAsync(date, dayEnd, cancellationToken);
        public Task<DateOnly?> FindDataStartAsync(CancellationToken cancellationToken) => inner.FindDataStartAsync(cancellationToken);
        public Task<IReadOnlySet<DateOnly>> ListMetricDatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) => inner.ListMetricDatesAsync(from, to, cancellationToken);
        public Task<bool> InsertPartialIfMissingAsync(DateOnly date, DailyFacts facts, CancellationToken cancellationToken) => inner.InsertPartialIfMissingAsync(date, facts, cancellationToken);
        public Task<IAsyncDisposable?> TryAcquireBackfillLockAsync(CancellationToken cancellationToken) => inner.TryAcquireBackfillLockAsync(cancellationToken);
    }
}

[CollectionDefinition("PostgresAislado", DisableParallelization = true)]
public sealed class PostgresAisladoCollection;
