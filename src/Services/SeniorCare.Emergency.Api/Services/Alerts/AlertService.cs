using SeniorCare.Emergency.Api.Domain;
using SeniorCare.Emergency.Api.Repositories;

namespace SeniorCare.Emergency.Api.Services.Alerts;

public sealed class AlertService(IEmergencyRepository alerts)
{
    private static readonly string[] AllowedTypes = ["assistance", "emergency"];
    private static readonly string[] AllowedStatuses = ["acknowledged", "resolved"];

    public bool IsValidType(string? type) =>
        !string.IsNullOrWhiteSpace(type)
        && AllowedTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    public bool IsValidStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);

    public Task<Alert> CreateAsync(
        CreateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = request.Type.Trim().ToLowerInvariant();
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? normalizedType == "emergency"
                ? "La persona adulta mayor activó el botón SOS."
                : "La persona adulta mayor solicitó asistencia."
            : request.Message.Trim();

        return alerts.CreateAsync(
            request with { Type = normalizedType, Message = message },
            cancellationToken);
    }

    public Task<IReadOnlyList<Alert>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        alerts.ListActiveAsync(cancellationToken);

    public Task<IReadOnlyList<Alert>> ListForResidentAsync(
        Guid residentId,
        CancellationToken cancellationToken = default) =>
        alerts.ListForResidentAsync(residentId, cancellationToken);

    public Task<Alert?> UpdateStatusAsync(
        Guid alertId,
        string status,
        string updatedBy,
        CancellationToken cancellationToken = default) =>
        alerts.UpdateStatusAsync(
            alertId,
            status.Trim().ToLowerInvariant(),
            updatedBy,
            cancellationToken);
}
