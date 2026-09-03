using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Identity;
using SeniorCare.Api.App.Services.Identity;

namespace SeniorCare.Api.Tests.Services.Identity;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_WithEmptyCredentials_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository();
        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("", ""));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Login);
        Assert.Null(repository.LastUsername);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownUser_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository();
        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("desconocido", "Password123!"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Login);
        Assert.Equal("desconocido", repository.LastUsername);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository
        {
            UserToFind = CreateUser(
                role: SeniorCareRoles.Admin,
                isActive: false)
        };

        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("admin", TestPassword));

        Assert.False(result.IsSuccess);
        Assert.False(repository.UpdateLastLoginCalled);
    }

    [Fact]
    public async Task LoginAsync_WithUnsupportedRole_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository
        {
            UserToFind = CreateUser(role: "unknown")
        };

        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("admin", TestPassword));

        Assert.False(result.IsSuccess);
        Assert.False(repository.UpdateLastLoginCalled);
    }

    [Fact]
    public async Task LoginAsync_ResidentWithoutResidentId_ReturnsFailure()
    {
        var repository = new FakeIdentityRepository
        {
            UserToFind = CreateUser(
                role: SeniorCareRoles.Resident,
                residentId: null)
        };

        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("admin", TestPassword));

        Assert.False(result.IsSuccess);
        Assert.False(repository.UpdateLastLoginCalled);
    }

    [Fact]
    public async Task LoginAsync_WithValidAdmin_ReturnsLoginAndUpdatesLastAccess()
    {
        var user = CreateUser(role: SeniorCareRoles.Admin);

        var repository = new FakeIdentityRepository
        {
            UserToFind = user
        };

        var service = CreateService(repository);

        var result = await service.LoginAsync(
            new LoginRequest("  admin  ", TestPassword));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Login);
        Assert.Equal("admin", repository.LastUsername);
        Assert.True(repository.UpdateLastLoginCalled);
        Assert.Equal(user.Id, repository.LastUpdatedUserId);
        Assert.Equal(
            "/panel/administracion/",
            result.Login!.PanelPath);
        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Login.AccessToken));
    }

    private const string TestPassword =
        "SeniorCare-Test-Password-2026!";

    private static AuthenticationService CreateService(
        FakeIdentityRepository repository)
    {
        var hasher = new PasswordHasher();

        return new AuthenticationService(
            repository,
            hasher,
            new TokenService(
                new JwtOptions
                {
                    Issuer = "SeniorCare.Tests",
                    Audience = "SeniorCare.Tests.Client",
                    Secret =
                        "SeniorCare-Test-Secret-Key-2026-At-Least-32-Bytes",
                    ExpirationMinutes = 60
                }),
            new RolePanelService());
    }

    private static UserAccount CreateUser(
        string role,
        bool isActive = true,
        Guid? residentId = null)
    {
        var hasher = new PasswordHasher();

        return new UserAccount(
            Guid.NewGuid(),
            "admin",
            "Administrador SeniorCare",
            hasher.Hash(TestPassword),
            role,
            residentId,
            isActive,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class FakeIdentityRepository : IIdentityRepository
    {
        public UserAccount? UserToFind { get; init; }

        public string? LastUsername { get; private set; }

        public bool UpdateLastLoginCalled { get; private set; }

        public Guid? LastUpdatedUserId { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserAccount?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            LastUsername = username;
            return Task.FromResult(UserToFind);
        }

        public Task<IReadOnlyList<PublicUser>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublicUser>>(
                Array.Empty<PublicUser>());

        public Task<PublicUser> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset> UpdateLastLoginAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            UpdateLastLoginCalled = true;
            LastUpdatedUserId = userId;

            return Task.FromResult(
                DateTimeOffset.UtcNow);
        }
    }
}
