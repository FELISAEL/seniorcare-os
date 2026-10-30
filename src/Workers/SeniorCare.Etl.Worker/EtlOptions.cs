namespace SeniorCare.Etl.Worker;

/// <summary>
/// Configuración del ETL. Cada valor admite la clave de appsettings (Etl:...) o la
/// variable de entorno equivalente (ETL_...), como ya ocurría con el intervalo.
/// </summary>
public sealed record EtlOptions(
    TimeSpan Interval,
    TimeZoneInfo TimeZone,
    bool BackfillEnabled,
    int BackfillDays,
    int BackfillBatchDays)
{
    public const string DefaultTimeZoneId = "America/Guatemala";

    public static EtlOptions FromConfiguration(IConfiguration configuration)
    {
        var intervalSeconds = ReadInt(configuration, "Etl:IntervalSeconds", "ETL_INTERVAL_SECONDS") ?? 30;
        var timeZoneId = Read(configuration, "Etl:TimeZone", "ETL_TIME_ZONE") ?? DefaultTimeZoneId;
        var backfillEnabled = ReadBool(configuration, "Etl:Backfill:Enabled", "ETL_BACKFILL_ENABLED") ?? false;
        var backfillDays = ReadInt(configuration, "Etl:Backfill:Days", "ETL_BACKFILL_DAYS") ?? 30;
        var batchDays = ReadInt(configuration, "Etl:Backfill:BatchDays", "ETL_BACKFILL_BATCH_DAYS") ?? 7;

        return new EtlOptions(
            TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 10, 3600)),
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId),
            backfillEnabled,
            Math.Clamp(backfillDays, 1, 365),
            Math.Clamp(batchDays, 1, 31));
    }

    /// <summary>Fecha local (según la zona configurada) de un instante.</summary>
    public DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

    /// <summary>Instante en que comienza una fecha local (medianoche en la zona configurada).</summary>
    public DateTimeOffset StartOfDay(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localMidnight, TimeZone.GetUtcOffset(localMidnight));
    }

    private static string? Read(IConfiguration configuration, string key, string environmentKey) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : configuration[environmentKey] is { Length: > 0 } environmentValue ? environmentValue : null;

    private static int? ReadInt(IConfiguration configuration, string key, string environmentKey) =>
        int.TryParse(Read(configuration, key, environmentKey), out var value) ? value : null;

    private static bool? ReadBool(IConfiguration configuration, string key, string environmentKey) =>
        bool.TryParse(Read(configuration, key, environmentKey), out var value) ? value : null;
}
