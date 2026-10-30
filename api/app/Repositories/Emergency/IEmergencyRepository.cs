using SeniorCare.Api.App.Models.Emergency;

namespace SeniorCare.Api.App.Repositories.Emergency;

public interface IEmergencyRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta el aviso. Si ya existe uno con el mismo ClientRequestId devuelve el existente
    /// con Created = false, sin insertar otro. reportedAt es la hora tal como la envió el dispositivo.
    /// </summary>
    Task<AlertCreationResult> CreateAsync(Alert alert, DateTimeOffset? reportedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListForResidentAsync(Guid residentId, CancellationToken cancellationToken = default);
    Task<Alert?> UpdateStatusAsync(Guid alertId, string status, string updatedBy, CancellationToken cancellationToken = default);
}
