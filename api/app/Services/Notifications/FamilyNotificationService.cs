using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Models.Emergency;
using SeniorCare.Api.App.Repositories.Care;

namespace SeniorCare.Api.App.Services.Notifications;

/// <summary>
/// Avisa por correo al familiar registrado del residente. Nunca lanza
/// excepciones ni retrasa la respuesta de la API: si el correo falla, solo se
/// registra en el log.
/// </summary>
public sealed class FamilyNotificationService(
    ICareRepository care,
    IEmailSender sender,
    ILogger<FamilyNotificationService> logger)
{
    public bool CanNotify => sender.IsConfigured;

    public void VideoCallRequested(Resident resident, string roomUrl, DateTimeOffset when)
    {
        if (string.IsNullOrWhiteSpace(resident.FamilyEmail))
        {
            return;
        }

        Dispatch(token => sender.SendAsync(
            EmailTemplates.VideoCallRequested(
                resident.FamilyEmail,
                resident.FullName,
                roomUrl,
                when),
            token), "videollamada solicitada");
    }

    public void DoseConfirmed(
        Guid residentId,
        Guid scheduleId,
        DateOnly date,
        DateTimeOffset confirmedAt,
        string? confirmedBy) =>
        Dispatch(async token =>
        {
            var resident = await care.FindResidentByIdAsync(residentId, token);
            if (string.IsNullOrWhiteSpace(resident?.FamilyEmail))
            {
                return;
            }

            var schedules = await care.GetTodayAsync(residentId, date, token);
            var schedule = schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);

            var registeredBy =
                string.IsNullOrWhiteSpace(confirmedBy) ? null
                : string.Equals(confirmedBy, resident.Username, StringComparison.OrdinalIgnoreCase)
                    ? "el propio residente"
                    : confirmedBy;

            await sender.SendAsync(
                EmailTemplates.DoseConfirmed(
                    resident.FamilyEmail,
                    resident.FullName,
                    schedule?.MedicationName ?? "su medicamento",
                    schedule?.Dosage ?? "-",
                    confirmedAt,
                    registeredBy),
                token);
        }, "dosis confirmada");

    public void AlertCreated(Alert alert) =>
        Dispatch(async token =>
        {
            var resident = await care.FindResidentByIdAsync(alert.ResidentId, token);
            if (string.IsNullOrWhiteSpace(resident?.FamilyEmail))
            {
                return;
            }

            await sender.SendAsync(
                EmailTemplates.AlertCreated(
                    resident.FamilyEmail,
                    resident.FullName,
                    alert.Type,
                    alert.Message,
                    alert.CreatedAt),
                token);
        }, "alerta creada");

    public void AlertStatusChanged(Alert alert) =>
        Dispatch(async token =>
        {
            var resident = await care.FindResidentByIdAsync(alert.ResidentId, token);
            if (string.IsNullOrWhiteSpace(resident?.FamilyEmail))
            {
                return;
            }

            await sender.SendAsync(
                EmailTemplates.AlertStatusChanged(
                    resident.FamilyEmail,
                    resident.FullName,
                    alert.Status,
                    alert.UpdatedBy ?? "el equipo de cuidado",
                    alert.UpdatedAt ?? DateTimeOffset.UtcNow),
                token);
        }, "estado de alerta");

    private void Dispatch(Func<CancellationToken, Task> work, string what)
    {
        if (!sender.IsConfigured)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await work(timeout.Token);
                logger.LogInformation("Notificación enviada al servidor de correo ({What}).", what);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "No se pudo enviar la notificación ({What}).", what);
            }
        });
    }
}
