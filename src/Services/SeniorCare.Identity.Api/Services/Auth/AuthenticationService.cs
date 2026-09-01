using SeniorCare.Identity.Api.Domain;
using SeniorCare.Identity.Api.Repositories.Auth;
using SeniorCare.Identity.Api.Security;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Identity.Api.Services.Auth;

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
                "Ingresá el usuario y la contraseña.",
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
                "Usuario o contraseña incorrectos.",
                null);
        }

        if ((user.Role.Equals(SeniorCareRoles.Resident, StringComparison.OrdinalIgnoreCase)
             || user.Role.Equals(SeniorCareRoles.Family, StringComparison.OrdinalIgnoreCase))
            && user.ResidentId is null)
        {
            return new AuthenticationResult(
                false,
                "La cuenta no está vinculada a una persona adulta mayor. Contactá al administrador.",
                null);
        }

        var lastLogin = await users.UpdateLastLoginAsync(user.Id, cancellationToken);
        var refreshedUser = user with { LastLoginAt = lastLogin };
        var login = tokens.Create(refreshedUser, panels.Resolve(refreshedUser.Role));

        return new AuthenticationResult(
            true,
            "Inicio de sesión correcto.",
            login);
    }
}
