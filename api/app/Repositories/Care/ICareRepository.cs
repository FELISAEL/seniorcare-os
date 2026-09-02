using SeniorCare.Api.App.Models.Care;

namespace SeniorCare.Api.App.Repositories.Care;

public interface ICareRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Resident>> ListResidentsAsync(
        CancellationToken cancellationToken = default);

    Task<Resident?> FindResidentByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task<Resident?> FindResidentByIdAsync(
        Guid residentId,
        CancellationToken cancellationToken = default);

    Task<Resident> CreateResidentAsync(
        CreateResidentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
        Guid residentId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<MedicationScheduleView> CreateMedicationAsync(
        Guid residentId,
        CreateMedicationRequest request,
        CancellationToken cancellationToken = default);

    Task<DoseConfirmation?> ConfirmDoseAsync(
        Guid residentId,
        Guid scheduleId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);
}
