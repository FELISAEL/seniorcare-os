using SeniorCare.Emergency.Api.Domain;

namespace SeniorCare.Emergency.Api.Repositories;

public interface IEmergencyRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<Alert> CreateAsync(CreateAlertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> ListForResidentAsync(Guid residentId, CancellationToken cancellationToken = default);
    Task<Alert?> UpdateStatusAsync(Guid alertId, string status, string updatedBy, CancellationToken cancellationToken = default);
}
