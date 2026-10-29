namespace SeniorCare.Api.App.Services.Notifications;

/// <summary>
/// Configuración del envío de correos (SMTP). Los valores se leen de variables
/// de entorno (.env): SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASSWORD, SMTP_FROM,
/// SMTP_FROM_NAME y SMTP_ENABLED. La contraseña NUNCA va en el repositorio.
/// </summary>
public sealed record EmailOptions(
    bool Enabled,
    string Host,
    int Port,
    string User,
    string Password,
    string From,
    string FromName)
{
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(User)
        && !string.IsNullOrWhiteSpace(Password);

    public static EmailOptions FromConfiguration(IConfiguration configuration)
    {
        static string Read(IConfiguration c, string env, string section, string fallback = "") =>
            c[env] ?? c[section] ?? fallback;

        var user = Read(configuration, "SMTP_USER", "Smtp:User").Trim();

        // Gmail muestra la contraseña de aplicación en grupos separados por espacios.
        var password = Read(configuration, "SMTP_PASSWORD", "Smtp:Password")
            .Replace(" ", string.Empty);

        var from = Read(configuration, "SMTP_FROM", "Smtp:From").Trim();
        if (from.Length == 0)
        {
            from = user;
        }

        _ = int.TryParse(
            Read(configuration, "SMTP_PORT", "Smtp:Port", "587"),
            out var port);

        var enabledText = Read(configuration, "SMTP_ENABLED", "Smtp:Enabled", "false");

        return new EmailOptions(
            bool.TryParse(enabledText, out var enabled) && enabled,
            Read(configuration, "SMTP_HOST", "Smtp:Host", "smtp.gmail.com").Trim(),
            port == 0 ? 587 : port,
            user,
            password,
            from,
            Read(configuration, "SMTP_FROM_NAME", "Smtp:FromName", "SeniorCare"));
    }
}
