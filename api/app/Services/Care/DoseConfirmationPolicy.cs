namespace SeniorCare.Api.App.Services.Care;

/// <summary>
/// Qué días se pueden confirmar como tomados.
///
/// Reglas actuales del sistema: el panel del Adulto Mayor solo muestra y confirma las dosis
/// de hoy, y ningún panel ofrece confirmar días anteriores. El único atraso legítimo es una
/// confirmación hecha sin conexión antes de medianoche que llega al servidor después; por eso
/// se acepta hoy y, por defecto, el día anterior (DOSE_CONFIRMATION_MAX_DAYS_LATE = 1).
/// Nunca se aceptan fechas futuras. Si el personal de cuidado necesita registrar días más
/// viejos, se amplía la variable (máximo 7) en lugar de aceptar cualquier fecha.
/// </summary>
public static class DoseConfirmationPolicy
{
    public const string SettingName = "DOSE_CONFIRMATION_MAX_DAYS_LATE";
    public const int DefaultMaxDaysLate = 1;
    public const int UpperLimitDaysLate = 7;

    public static int ReadMaxDaysLate(IConfiguration configuration)
    {
        var raw = configuration[SettingName];
        return int.TryParse(raw, out var value)
            ? Math.Clamp(value, 0, UpperLimitDaysLate)
            : DefaultMaxDaysLate;
    }

    /// <summary>Devuelve null si la fecha es válida, o el mensaje para el usuario si no.</summary>
    public static string? Validate(DateOnly requested, DateOnly today, int maxDaysLate)
    {
        if (requested > today)
        {
            return "No se puede confirmar una dosis de una fecha futura.";
        }

        if (requested < today.AddDays(-maxDaysLate))
        {
            return maxDaysLate == 0
                ? "Solo se pueden confirmar las dosis de hoy."
                : $"Solo se pueden confirmar dosis de hoy y de los últimos {maxDaysLate} día(s). "
                  + "Para fechas anteriores, avisá al personal de cuidado.";
        }

        return null;
    }
}
