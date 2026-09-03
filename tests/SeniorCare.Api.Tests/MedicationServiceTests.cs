using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Services.Care;

namespace SeniorCare.Api.Tests.Services.Care;

public sealed class MedicationServiceTests
{
    [Fact]
    public async Task GetTodayAsync_ForwardsResidentAndDate()
    {
        var repository = new FakeCareRepository();
        var service = new MedicationService(repository);
        var residentId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 2);

        await service.GetTodayAsync(residentId, date);

        Assert.Equal(residentId, repository.LastResidentId);
        Assert.Equal(date, repository.LastDate);
    }

    [Fact]
    public async Task CreateAsync_ForwardsResidentAndRequest()
    {
        var repository = new FakeCareRepository();
        var service = new MedicationService(repository);
        var residentId = Guid.NewGuid();
        var request = new CreateMedicationRequest(
            "Losartán",
            "50 mg",
            "Tomar después del desayuno",
            new TimeOnly(8, 0));

        await service.CreateAsync(residentId, request);

        Assert.Equal(residentId, repository.LastResidentId);
        Assert.Same(request, repository.LastMedicationRequest);
    }

    [Fact]
    public async Task ConfirmDoseAsync_ForwardsExpectedData()
    {
        var repository = new FakeCareRepository();
        var service = new MedicationService(repository);
        var residentId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 2);

        await service.ConfirmDoseAsync(
            residentId,
            scheduleId,
            date);

        Assert.Equal(residentId, repository.LastResidentId);
        Assert.Equal(scheduleId, repository.LastScheduleId);
        Assert.Equal(date, repository.LastDate);
    }

    private sealed class FakeCareRepository : ICareRepository
    {
        public Guid? LastResidentId { get; private set; }
        public Guid? LastScheduleId { get; private set; }
        public DateOnly? LastDate { get; private set; }
        public CreateMedicationRequest? LastMedicationRequest { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Resident>> ListResidentsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Resident>>(
                Array.Empty<Resident>());

        public Task<Resident?> FindResidentByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Resident?>(null);

        public Task<Resident?> FindResidentByIdAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Resident?>(null);

        public Task<Resident> CreateResidentAsync(
            CreateResidentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
            Guid residentId,
            DateOnly localDate,
            CancellationToken cancellationToken = default)
        {
            LastResidentId = residentId;
            LastDate = localDate;

            return Task.FromResult<IReadOnlyList<MedicationScheduleView>>(
                Array.Empty<MedicationScheduleView>());
        }

        public Task<MedicationScheduleView> CreateMedicationAsync(
            Guid residentId,
            CreateMedicationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastResidentId = residentId;
            LastMedicationRequest = request;

            return Task.FromResult(
                new MedicationScheduleView(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    request.Name,
                    request.Dosage,
                    request.Instructions,
                    request.Time,
                    "pending",
                    null));
        }

        public Task<DoseConfirmation?> ConfirmDoseAsync(
            Guid residentId,
            Guid scheduleId,
            DateOnly localDate,
            CancellationToken cancellationToken = default)
        {
            LastResidentId = residentId;
            LastScheduleId = scheduleId;
            LastDate = localDate;

            return Task.FromResult<DoseConfirmation?>(null);
        }
    }
}
