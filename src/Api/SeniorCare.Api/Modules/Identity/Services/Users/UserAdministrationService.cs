using SeniorCare.Api.Modules.Identity.Domain;
using SeniorCare.Api.Modules.Identity.Repositories.Auth;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Api.Modules.Identity.Services.Users;

public sealed class UserAdministrationService(IIdentityRepository users)
{
    public Task<IReadOnlyList<PublicUser>> ListAsync(
        CancellationToken cancellationToken = default) =>
        users.ListAsync(cancellationToken);

    public async Task<UserCreationResult> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var role = request.Role?.Trim().ToLowerInvariant() ?? string.Empty;

        if (username.Length < 3
            || username.Length > 80
            || displayName.Length < 3
            || displayName.Length > 160)
        {
            return new UserCreationResult(
                false,
                "RevisÃ¡ el usuario y el nombre completo.",
                null);
        }

        if (string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length < 8)
        {
            return new UserCreationResult(
                false,
                "La contraseÃ±a debe tener al menos 8 caracteres.",
                null);
        }

        if (!SeniorCareRoles.IsSupported(role))
        {
            return new UserCreationResult(
                false,
                "El rol indicado no existe en SeniorCare.",
                null);
        }

        var needsResidentLink = string.Equals(role, SeniorCareRoles.Resident, StringComparison.Ordinal)
            || string.Equals(role, SeniorCareRoles.Family, StringComparison.Ordinal);
        if (needsResidentLink && request.ResidentId is null)
        {
            return new UserCreationResult(
                false,
                "Los roles adulto mayor y familiar deben vincularse a un residente.",
                null);
        }

        if (!needsResidentLink && request.ResidentId is not null)
        {
            return new UserCreationResult(
                false,
                "Solo las cuentas de adulto mayor o familiar pueden tener residente vinculado.",
                null);
        }

        var existing = await users.FindByUsernameAsync(username, cancellationToken);
        if (existing is not null)
        {
            return new UserCreationResult(
                false,
                "El usuario ya existe.",
                null);
        }

        var normalized = request with
        {
            Username = username,
            DisplayName = displayName,
            Role = role
        };

        var created = await users.CreateAsync(normalized, cancellationToken);
        return new UserCreationResult(true, "Usuario creado correctamente.", created);
    }
}

