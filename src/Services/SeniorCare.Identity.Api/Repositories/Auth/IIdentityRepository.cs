using SeniorCare.Identity.Api.Domain;

namespace SeniorCare.Identity.Api.Repositories.Auth;

public interface IIdentityRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicUser>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<PublicUser> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset> UpdateLastLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
