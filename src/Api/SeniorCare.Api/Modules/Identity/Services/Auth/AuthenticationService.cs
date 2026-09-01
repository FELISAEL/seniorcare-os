using SeniorCare.Api.Modules.Identity.Domain;
using SeniorCare.Api.Modules.Identity.Repositories.Auth;
using SeniorCare.Api.Modules.Identity.Security;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Api.Modules.Identity.Services.Auth;

public sealed class AuthenticationService(
    IIdentityRepository users,
    PasswordHasher passwordHasher,
    TokenService tokens,
    RolePanelService panels)
{
    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            return new AuthenticationResult(
                false,
                "IngresÃ¡ el usuario y la contraseÃ±a.",
                null);
        }

        var user = await users.FindByUsernameAsync(username, cancellationToken);
        if (user is null
            || !user.IsActive
            || !SeniorCareRoles.IsSupported(user.Role)
            || !passwordHasher.Verify(password, user.PasswordHash))
        {
            return new AuthenticationResult(
                false,
                "Usuario o contraseÃ±a incorrectos.",
                null);
        }

        if ((user.Role.Equals(SeniorCareRoles.Resident, StringComparison.OrdinalIgnoreCase)
             || user.Role.Equals(SeniorCareRoles.Family, StringComparison.OrdinalIgnoreCase))
            && user.ResidentId is null)
        {
            return new AuthenticationResult(
                false,
                "La cuenta no estÃ¡ vinculada a una persona adulta mayor. ContactÃ¡ al administrador.",
                null);
        }

        var lastLogin = await users.UpdateLastLoginAsync(user.Id, cancellationToken);
        var refreshedUser = user with { LastLoginAt = lastLogin };
        var login = tokens.Create(refreshedUser, panels.Resolve(refreshedUser.Role));

        return new AuthenticationResult(
            true,
            "Inicio de sesiÃ³n correcto.",
            login);
    }
}

