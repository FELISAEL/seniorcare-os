namespace SeniorCare.Etl.Worker;

/// <summary>Decide qué fechas históricas recuperar en un ciclo. Sin acceso a datos: se prueba aislado.</summary>
public static class BackfillPlanner
{
    /// <summary>
    /// Rango recuperable: desde el inicio de la ventana (o el primer dato de origen, si es posterior)
    /// hasta ayer. Hoy nunca entra: lo calcula el ETL periódico. Null si no hay nada que revisar.
    /// </summary>
    public static (DateOnly From, DateOnly To)? ResolveRange(
        DateOnly today,
        DateOnly? dataStart,
        int windowDays)
    {
        if (dataStart is null)
        {
            return null;
        }

        var windowStart = today.AddDays(-windowDays);
        var from = dataStart.Value > windowStart ? dataStart.Value : windowStart;
        var to = today.AddDays(-1);
        return from <= to ? (from, to) : null;
    }

    /// <summary>Días de la ventana anteriores al primer dato de origen: sin información, no se escriben.</summary>
    public static int DaysWithoutSourceData(DateOnly today, DateOnly? dataStart, int windowDays)
    {
        var windowStart = today.AddDays(-windowDays);
        var firstUsable = dataStart ?? today;
        return Math.Max(0, firstUsable.DayNumber - windowStart.DayNumber);
    }

    /// <summary>Fechas sin métricas, en orden cronológico y limitadas al tamaño de lote.</summary>
    public static IReadOnlyList<DateOnly> PendingDates(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<DateOnly> existing,
        int batchDays)
    {
        var pending = new List<DateOnly>(batchDays);
        for (var date = from; date <= to && pending.Count < batchDays; date = date.AddDays(1))
        {
            if (!existing.Contains(date))
            {
                pending.Add(date);
            }
        }

        return pending;
    }

    /// <summary>Total de fechas sin métricas en el rango (para informar lo que queda pendiente).</summary>
    public static int CountMissing(DateOnly from, DateOnly to, IReadOnlySet<DateOnly> existing) =>
        to.DayNumber - from.DayNumber + 1 - existing.Count(date => date >= from && date <= to);
}
