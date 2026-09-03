using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Identity;
using SeniorCare.Api.App.Services.Identity;

namespace SeniorCare.Api.Tests.Services.Identity;

public sealed class UserAdministrationServiceTests
{
    [Fact]
    public async Task CreateAsync_WithInvalidUsername_ReturnsFailure()
    {
        var service = new UserAdministrationService(
            new FakeIdentityRepository());

        var result = await service.CreateAsync(
            CreateRequest(username: "ab"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task CreateAsync_WithShortPassword_ReturnsFailure()
    {
        var service = new UserAdministrationService(
            new FakeIdentityRepository());

        var result = await service.CreateAsync(
            CreateRequest(password: "1234567"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_WithUnsupportedRole_ReturnsFailure()
    {
        var service = new UserAdministrationService(
            new FakeIdentityRepository());

        var result = await service.CreateAsync(
            CreateRequest(role: "unknown"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_ResidentWithoutResidentId_ReturnsFailure()
    {
        var service = new UserAdministrationService(
            new FakeIdentityRepository());

        var result = await service.CreateAsync(
            CreateRequest(
                role: SeniorCareRoles.Resident,
                residentId: null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_AdminWithResidentId_ReturnsFailure()
    {
        var service = new UserAdministrationService(
            new FakeIdentityRepository());

        var result = await service.CreateAsync(
            CreateRequest(
                role: SeniorCareRoles.Admin,
                residentId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_WithExistingUsername_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository
        {
            ExistingUser = CreateAccount()
        };

        var service = new UserAdministrationService(repository);

        var result = await service.CreateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.False(repository.CreateCalled);
    }

    [Fact]
    public async Task CreateAsync_WithValidAdmin_NormalizesAndCreatesUser()
    {
        var repository = new FakeIdentityRepository();
        var service = new UserAdministrationService(repository);

        var result = await service.CreateAsync(
            CreateRequest(
                username: "  admin2  ",
                displayName: "  Administrador Dos  ",
                role: " ADMIN "));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.True(repository.CreateCalled);
        Assert.Equal("admin2", repository.LastCreatedRequest!.Username);
        Assert.Equal(
            "Administrador Dos",
            repository.LastCreatedRequest.DisplayName);
        Assert.Equal("admin", repository.LastCreatedRequest.Role);
    }

    [Fact]
    public async Task CreateAsync_WithValidFamilyAndResident_CreatesUser()
    {
        var repository = new FakeIdentityRepository();
        var service = new UserAdministrationService(repository);
        var residentId = Guid.NewGuid();

        var result = await service.CreateAsync(
            CreateRequest(
                role: SeniorCareRoles.Family,
                residentId: residentId));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            residentId,
            repository.LastCreatedRequest!.ResidentId);
    }

    private static CreateUserRequest CreateRequest(
        string username = "admin2",
        string displayName = "Administrador Dos",
        string password = "SeniorCare123!",
        string role = SeniorCareRoles.Admin,
        Guid? residentId = null) =>
        new(
            username,
            displayName,
            password,
            role,
            residentId);

    private static UserAccount CreateAccount() =>
        new(
            Guid.NewGuid(),
            "admin2",
            "Administrador Dos",
            "hash",
            SeniorCareRoles.Admin,
            null,
            true,
            DateTimeOffset.UtcNow,
            null);

    private sealed class FakeIdentityRepository : IIdentityRepository
    {
        public UserAccount? ExistingUser { get; init; }

        public bool CreateCalled { get; private set; }

        public CreateUserRequest? LastCreatedRequest { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserAccount?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingUser);

        public Task<IReadOnlyList<PublicUser>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublicUser>>(
                Array.Empty<PublicUser>());

        public Task<PublicUser> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCalled = true;
            LastCreatedRequest = request;

            return Task.FromResult(
                new PublicUser(
                    Guid.NewGuid(),
                    request.Username,
                    request.DisplayName,
                    request.Role,
                    request.ResidentId,
                    true,
                    DateTimeOffset.UtcNow,
                    null));
        }

        public Task<DateTimeOffset> UpdateLastLoginAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DateTimeOffset.UtcNow);
    }
}
