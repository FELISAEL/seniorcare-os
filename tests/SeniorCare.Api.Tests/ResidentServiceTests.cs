using System.Security.Claims;
using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Services.Care;

namespace SeniorCare.Api.Tests.Services.Care;

public sealed class ResidentServiceTests
{
    [Fact]
    public async Task ListForCurrentUserAsync_CareTeam_ReturnsAllResidents()
    {
        var resident = CreateResident();
        var repository = new FakeCareRepository
        {
            ResidentsToList = [resident]
        };

        var service = new ResidentService(repository);
        var principal = CreatePrincipal("admin");

        var result = await service.ListForCurrentUserAsync(principal);

        Assert.Single(result);
        Assert.Equal(resident.Id, result[0].Id);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_Resident_ReturnsOwnResident()
    {
        var resident = CreateResident();
        var repository = new FakeCareRepository
        {
            ResidentToFind = resident
        };

        var service = new ResidentService(repository);
        var principal = CreatePrincipal("resident", resident.Id);

        var result = await service.ListForCurrentUserAsync(principal);

        Assert.Single(result);
        Assert.Equal(resident.Id, result[0].Id);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_WithoutResidentId_ReturnsEmpty()
    {
        var service = new ResidentService(new FakeCareRepository());
        var principal = CreatePrincipal("family");

        var result = await service.ListForCurrentUserAsync(principal);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_WhenResidentDoesNotExist_ReturnsEmpty()
    {
        var service = new ResidentService(new FakeCareRepository());
        var principal = CreatePrincipal("resident", Guid.NewGuid());

        var result = await service.ListForCurrentUserAsync(principal);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByUsernameAsync_ForwardsUsername()
    {
        var repository = new FakeCareRepository();
        var service = new ResidentService(repository);

        await service.FindByUsernameAsync("maria");

        Assert.Equal("maria", repository.LastUsername);
    }

    [Fact]
    public async Task CreateAsync_ForwardsRequest()
    {
        var repository = new FakeCareRepository();
        var service = new ResidentService(repository);

        var request = new CreateResidentRequest(
            "maria",
            "María López",
            new DateOnly(1948, 4, 12),
            "Ana López",
            "+502 5555-0101");

        await service.CreateAsync(request);

        Assert.Equal(request, repository.LastCreateRequest);
    }

    private static Resident CreateResident() =>
        new(
            Guid.NewGuid(),
            "maria",
            "María López",
            new DateOnly(1948, 4, 12),
            "Ana López",
            "+502 5555-0101",
            DateTimeOffset.UtcNow);

    private static ClaimsPrincipal CreatePrincipal(
        string role,
        Guid? residentId = null)
    {
        var claims = new List<Claim>
        {
            new("role", role)
        };

        if (residentId.HasValue)
        {
            claims.Add(
                new Claim(
                    "resident_id",
                    residentId.Value.ToString()));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                "Test",
                "name",
                "role"));
    }

    private sealed class FakeCareRepository : ICareRepository
    {
        public IReadOnlyList<Resident> ResidentsToList { get; init; } =
            Array.Empty<Resident>();

        public Resident? ResidentToFind { get; init; }

        public string? LastUsername { get; private set; }

        public CreateResidentRequest? LastCreateRequest { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Resident>> ListResidentsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ResidentsToList);

        public Task<Resident?> FindResidentByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            LastUsername = username;
            return Task.FromResult(ResidentToFind);
        }

        public Task<Resident?> FindResidentByIdAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ResidentToFind);

        public Task<Resident> CreateResidentAsync(
            CreateResidentRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;

            return Task.FromResult(
                new Resident(
                    Guid.NewGuid(),
                    request.Username,
                    request.FullName,
                    request.BirthDate,
                    request.EmergencyContactName,
                    request.EmergencyContactPhone,
                    DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
            Guid residentId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicationScheduleView>>(
                Array.Empty<MedicationScheduleView>());

        public Task<MedicationScheduleView> CreateMedicationAsync(
            Guid residentId,
            CreateMedicationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DoseConfirmation?> ConfirmDoseAsync(
            Guid residentId,
            Guid scheduleId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DoseConfirmation?>(null);
    }
}
