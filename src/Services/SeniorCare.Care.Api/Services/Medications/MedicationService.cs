using SeniorCare.Care.Api.Domain;
using SeniorCare.Care.Api.Repositories;

namespace SeniorCare.Care.Api.Services.Medications;

public sealed class MedicationService(ICareRepository care)
{
    public Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
        Guid residentId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        care.GetTodayAsync(residentId, date, cancellationToken);

    public Task<MedicationScheduleView> CreateAsync(
        Guid residentId,
        CreateMedicationRequest request,
        CancellationToken cancellationToken = default) =>
        care.CreateMedicationAsync(residentId, request, cancellationToken);

    public Task<DoseConfirmation?> ConfirmDoseAsync(
        Guid residentId,
        Guid scheduleId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        care.ConfirmDoseAsync(residentId, scheduleId, date, cancellationToken);
}
