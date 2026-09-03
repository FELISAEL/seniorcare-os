using SeniorCare.Api.App.Models.Emergency;
using SeniorCare.Api.App.Repositories.Emergency;
using SeniorCare.Api.App.Services.Emergency;

namespace SeniorCare.Api.Tests.Services.Emergency;

public sealed class AlertServiceTests
{
    [Fact]
    public void IsValidType_RecognizesAllowedTypes()
    {
        var service = new AlertService(new FakeEmergencyRepository());

        Assert.True(service.IsValidType("assistance"));
        Assert.True(service.IsValidType(" EMERGENCY "));
        Assert.False(service.IsValidType("other"));
        Assert.False(service.IsValidType(null));
    }

    [Fact]
    public void IsValidStatus_RecognizesAllowedStatuses()
    {
        var service = new AlertService(new FakeEmergencyRepository());

        Assert.True(service.IsValidStatus("acknowledged"));
        Assert.True(service.IsValidStatus(" RESOLVED "));
        Assert.False(service.IsValidStatus("active"));
        Assert.False(service.IsValidStatus(null));
    }

    [Fact]
    public async Task CreateAsync_NormalizesTypeAndMessage()
    {
        var repository = new FakeEmergencyRepository();
        var service = new AlertService(repository);

        await service.CreateAsync(
            new CreateAlertRequest(
                Guid.NewGuid(),
                "María López",
                " EMERGENCY ",
                "  Necesita ayuda inmediata.  "));

        Assert.NotNull(repository.LastCreateRequest);
        Assert.Equal("emergency", repository.LastCreateRequest!.Type);
        Assert.Equal(
            "Necesita ayuda inmediata.",
            repository.LastCreateRequest.Message);
    }

    [Fact]
    public async Task CreateAsync_WithBlankMessage_GeneratesDefaultMessage()
    {
        var repository = new FakeEmergencyRepository();
        var service = new AlertService(repository);

        await service.CreateAsync(
            new CreateAlertRequest(
                Guid.NewGuid(),
                "María López",
                "assistance",
                "   "));

        Assert.NotNull(repository.LastCreateRequest);
        Assert.Equal("assistance", repository.LastCreateRequest!.Type);
        Assert.False(
            string.IsNullOrWhiteSpace(
                repository.LastCreateRequest.Message));
    }

    [Fact]
    public async Task UpdateStatusAsync_NormalizesStatus()
    {
        var repository = new FakeEmergencyRepository();
        var service = new AlertService(repository);
        var alertId = Guid.NewGuid();

        await service.UpdateStatusAsync(
            alertId,
            " RESOLVED ",
            "admin");

        Assert.Equal("resolved", repository.LastStatus);
        Assert.Equal("admin", repository.LastUpdatedBy);
    }

    private sealed class FakeEmergencyRepository : IEmergencyRepository
    {
        public CreateAlertRequest? LastCreateRequest { get; private set; }
        public string? LastStatus { get; private set; }
        public string? LastUpdatedBy { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Alert> CreateAsync(
            CreateAlertRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;

            return Task.FromResult(
                new Alert(
                    Guid.NewGuid(),
                    request.ResidentId,
                    request.ResidentName,
                    request.Type,
                    request.Message,
                    "active",
                    DateTimeOffset.UtcNow,
                    null,
                    null));
        }

        public Task<IReadOnlyList<Alert>> ListActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Alert>>(
                Array.Empty<Alert>());

        public Task<IReadOnlyList<Alert>> ListForResidentAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Alert>>(
                Array.Empty<Alert>());

        public Task<Alert?> UpdateStatusAsync(
            Guid alertId,
            string status,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            LastStatus = status;
            LastUpdatedBy = updatedBy;

            return Task.FromResult<Alert?>(null);
        }
    }
}
