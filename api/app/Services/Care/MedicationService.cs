using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Services.Notifications;

namespace SeniorCare.Api.App.Services.Care;

public sealed class MedicationService(
    ICareRepository care,
    FamilyNotificationService? notifier = null)
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

    public async Task<DoseConfirmation?> ConfirmDoseAsync(
        Guid residentId,
        Guid scheduleId,
        DateOnly date,
        CancellationToken cancellationToken = default,
        string? confirmedBy = null)
    {
        var confirmation = await care.ConfirmDoseAsync(
            residentId,
            scheduleId,
            date,
            cancellationToken);

        if (confirmation is not null)
        {
            notifier?.DoseConfirmed(
                residentId,
                scheduleId,
                date,
                confirmation.ConfirmedAt,
                confirmedBy);
        }

        return confirmation;
    }
}
