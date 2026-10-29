using System.Net;
using System.Text;

namespace SeniorCare.Api.App.Services.Notifications;

/// <summary>
/// Formato de todos los correos que envía SeniorCare.
/// Asunto: "SeniorCare · {resumen}". Cuerpo: título, datos clave (tabla) y pie.
/// </summary>
public static class EmailTemplates
{
    // Paleta de la pantalla de inicio de sesión del portal (variables.css).
    private const string Teal = "#0b817e";      // --login-primary
    private const string Red = "#ba3c4d";       // --login-danger
    private const string Deep = "#083f47";      // --login-primary-deep
    private const string Text = "#12383e";      // --login-text
    private const string TextSoft = "#667a7f";  // --login-text-soft
    private const string Border = "#d9e8e7";    // --login-border
    private const string Page = "#f4f9f9";      // --login-background
    private const string Light = "#e5f5f3";     // --login-primary-light
    private const string Accent = "#e8a35b";    // --login-accent

    public static EmailMessage DoseConfirmed(
        string to,
        string residentName,
        string medication,
        string dosage,
        DateTimeOffset confirmedAt,
        string? registeredBy)
    {
        var rows = new List<(string, string)>
        {
            ("Residente", residentName),
            ("Medicamento", medication),
            ("Dosis", dosage),
            ("Hora", FormatTime(confirmedAt))
        };

        if (!string.IsNullOrWhiteSpace(registeredBy))
        {
            rows.Add(("Registrado por", registeredBy));
        }

        return Build(
            to,
            $"SeniorCare · {residentName} tomó su medicamento",
            "Medicamento tomado",
            $"{residentName} confirmó que tomó su medicamento.",
            rows,
            Teal);
    }

    public static EmailMessage AlertCreated(
        string to,
        string residentName,
        string type,
        string message,
        DateTimeOffset createdAt)
    {
        var emergency = string.Equals(type, "emergency", StringComparison.OrdinalIgnoreCase);

        return Build(
            to,
            emergency
                ? $"🚨 SeniorCare · ALERTA SOS de {residentName}"
                : $"SeniorCare · {residentName} solicitó asistencia",
            emergency ? "ALERTA SOS" : "Solicitud de asistencia",
            emergency
                ? $"{residentName} activó el botón SOS. El equipo de cuidado ya fue avisado."
                : $"{residentName} solicitó asistencia al equipo de cuidado.",
            [
                ("Residente", residentName),
                ("Tipo", emergency ? "Emergencia (SOS)" : "Asistencia"),
                ("Mensaje", message),
                ("Hora", FormatTime(createdAt))
            ],
            emergency ? Red : Teal);
    }

    public static EmailMessage AlertStatusChanged(
        string to,
        string residentName,
        string status,
        string updatedBy,
        DateTimeOffset when)
    {
        var resolved = string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase);
        var statusText = resolved ? "Resuelta" : "Atendida";

        return Build(
            to,
            $"SeniorCare · Alerta de {residentName}: {statusText.ToLowerInvariant()}",
            $"Alerta {statusText.ToLowerInvariant()}",
            resolved
                ? $"La alerta de {residentName} fue resuelta."
                : $"El equipo de cuidado está atendiendo la alerta de {residentName}.",
            [
                ("Residente", residentName),
                ("Estado", statusText),
                ("Atendida por", updatedBy),
                ("Hora", FormatTime(when))
            ],
            Teal);
    }

    public static EmailMessage VideoCallRequested(
        string to,
        string residentName,
        string roomUrl,
        DateTimeOffset when) =>
        Build(
            to,
            $"SeniorCare · {residentName} quiere hacer una videollamada",
            "Videollamada solicitada",
            $"{residentName} quiere hablar contigo. La llamada empezará cuando te conectes.",
            [
                ("Residente", residentName),
                ("Hora", FormatTime(when))
            ],
            Teal,
            ("Unirme a la videollamada", roomUrl));

    public static EmailMessage Test(string to) =>
        Build(
            to,
            "SeniorCare · Correo de prueba",
            "Correo de prueba",
            "Si recibes este mensaje, las notificaciones por correo de SeniorCare están funcionando.",
            [("Hora", FormatTime(DateTimeOffset.UtcNow))],
            Teal);

    public static string FormatTime(DateTimeOffset value)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala");
        }
        catch (Exception)
        {
            zone = TimeZoneInfo.CreateCustomTimeZone("GT", TimeSpan.FromHours(-6), "GT", "GT");
        }

        return TimeZoneInfo.ConvertTime(value, zone).ToString("dd/MM/yyyy HH:mm");
    }

    private static EmailMessage Build(
        string to,
        string subject,
        string title,
        string intro,
        IReadOnlyList<(string Label, string Value)> rows,
        string color,
        (string Text, string Url)? button = null)
    {
        var html = new StringBuilder();
        html.Append($"<div style=\"background:{Page};padding:24px 12px;font-family:Segoe UI,Arial,sans-serif\">");
        html.Append($"<div style=\"max-width:520px;margin:auto;background:#ffffff;border:1px solid {Border};border-radius:16px;overflow:hidden\">");
        html.Append($"<div style=\"background:{color};{(color == Teal ? $"background-image:linear-gradient(135deg,{Deep},{Teal});" : string.Empty)}color:#ffffff;padding:18px 22px\">");
        html.Append("<div style=\"font-size:13px;letter-spacing:.08em;text-transform:uppercase;opacity:.85\">SeniorCare</div>");
        html.Append($"<div style=\"font-size:20px;font-weight:700;margin-top:2px\">{Encode(title)}</div></div>");
        html.Append($"<div style=\"height:4px;background:{Accent}\"></div>");
        html.Append($"<div style=\"padding:22px;color:{Text}\">");
        html.Append($"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.5\">{Encode(intro)}</p>");
        html.Append($"<table style=\"width:100%;border-collapse:collapse;font-size:14px;background:{Page};border-radius:10px\">");
        foreach (var (label, value) in rows)
        {
            html.Append($"<tr><td style=\"padding:9px 12px;color:{TextSoft};width:38%;border-bottom:1px solid {Border}\">{Encode(label)}</td><td style=\"padding:9px 12px;font-weight:600;color:{Text};border-bottom:1px solid {Border}\">{Encode(value)}</td></tr>");
        }

        html.Append("</table>");
        if (button is { } action)
        {
            html.Append($"<p style=\"margin:22px 0 0\"><a href=\"{Encode(action.Url)}\" style=\"display:inline-block;background:{color};color:#ffffff;text-decoration:none;padding:13px 24px;border-radius:999px;font-weight:700;font-size:15px\">{Encode(action.Text)}</a></p>");
        }

        html.Append("</div>");
        html.Append($"<div style=\"background:{Light};color:{TextSoft};padding:12px 22px;font-size:12px;line-height:1.5\">Mensaje automático de SeniorCare. Recibes este correo porque fuiste registrado como familiar. No respondas a este mensaje.</div>");
        html.Append("</div></div>");

        var text = new StringBuilder();
        text.AppendLine($"SeniorCare · {title}");
        text.AppendLine();
        text.AppendLine(intro);
        text.AppendLine();
        foreach (var (label, value) in rows)
        {
            text.AppendLine($"{label}: {value}");
        }

        if (button is { } link)
        {
            text.AppendLine();
            text.AppendLine($"{link.Text}: {link.Url}");
        }

        text.AppendLine();
        text.AppendLine("Mensaje automático de SeniorCare. No respondas a este mensaje.");

        return new EmailMessage(to, subject, html.ToString(), text.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
