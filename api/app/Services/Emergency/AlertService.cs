using SeniorCare.Api.App.Models.Emergency;
using SeniorCare.Api.App.Repositories.Emergency;
using SeniorCare.Api.App.Services.Notifications;

namespace SeniorCare.Api.App.Services.Emergency;

public sealed class AlertService(
    IEmergencyRepository alerts,
    FamilyNotificationService? notifier = null,
    TimeProvider? clock = null)
{
    private static readonly string[] AllowedTypes = ["assistance", "emergency"];
    private static readonly string[] AllowedStatuses = ["acknowledged", "resolved"];

    // La hora del dispositivo se respeta si es creíble. Un aviso nunca se rechaza por su hora:
    // si el reloj del dispositivo está adelantado o la hora es demasiado vieja, se registra
    // la hora de llegada y la hora informada queda guardada aparte (reported_at).
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MaxQueuedAge = TimeSpan.FromHours(72);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public bool IsValidType(string? type) =>
        !string.IsNullOrWhiteSpace(type)
        && AllowedTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    public bool IsValidStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);

    public static DateTimeOffset ResolveOccurredAt(
        DateTimeOffset? reported,
        DateTimeOffset receivedAt)
    {
        if (reported is not { } value)
        {
            return receivedAt;
        }

        var tooNew = value > receivedAt + MaxClockSkew;
        var tooOld = value < receivedAt - MaxQueuedAge;
        if (tooNew || tooOld)
        {
            return receivedAt;
        }

        // Una pequeña diferencia de reloj hacia adelante no puede dejar el evento en el futuro.
        return value > receivedAt ? receivedAt : value;
    }

    public async Task<AlertCreationResult> CreateAsync(
        CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = request.Type.Trim().ToLowerInvariant();
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? normalizedType == "emergency"
                ? "La persona adulta mayor activó el botón SOS."
                : "La persona adulta mayor solicitó asistencia."
            : request.Message.Trim();

        var receivedAt = _clock.GetUtcNow();
        var occurredAt = ResolveOccurredAt(request.OccurredAt, receivedAt);
        var clientRequestId = request.ClientRequestId is { } id && id != Guid.Empty
            ? id
            : (Guid?)null;

        var result = await alerts.CreateAsync(
            new Alert(
                Guid.NewGuid(),
                request.ResidentId,
                request.ResidentName.Trim(),
                normalizedType,
                message,
                "active",
                occurredAt,
                null,
                null,
                receivedAt,
                clientRequestId),
            request.OccurredAt,
            cancellationToken);

        // Un reintento del mismo aviso no vuelve a notificar.
        if (result.Created)
        {
            notifier?.AlertCreated(result.Alert);
        }

        return result;
    }

    public Task<IReadOnlyList<Alert>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        alerts.ListActiveAsync(cancellationToken);

    public Task<IReadOnlyList<Alert>> ListForResidentAsync(
        Guid residentId,
        CancellationToken cancellationToken = default) =>
        alerts.ListForResidentAsync(residentId, cancellationToken);

    public async Task<Alert?> UpdateStatusAsync(
        Guid alertId,
        string status,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var updated = await alerts.UpdateStatusAsync(
            alertId,
            status.Trim().ToLowerInvariant(),
            updatedBy,
            cancellationToken);

        if (updated is not null)
        {
            notifier?.AlertStatusChanged(updated);
        }

        return updated;
    }
}
