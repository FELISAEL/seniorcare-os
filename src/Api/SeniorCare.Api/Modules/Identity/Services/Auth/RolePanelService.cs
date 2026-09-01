using SeniorCare.Shared.Auth;

namespace SeniorCare.Api.Modules.Identity.Services.Auth;

public sealed class RolePanelService
{
    private static readonly IReadOnlyDictionary<string, string> PanelByRole =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SeniorCareRoles.Admin] = "/panel/administracion/",
            [SeniorCareRoles.Caregiver] = "/panel/cuidador/",
            [SeniorCareRoles.Resident] = "/panel/adulto-mayor/",
            [SeniorCareRoles.Family] = "/panel/familiar/"
        };

    public string Resolve(string role)
    {
        if (PanelByRole.TryGetValue(role.Trim(), out var panelPath))
        {
            return panelPath;
        }

        throw new InvalidOperationException(
            $"El rol '{role}' no tiene un panel configurado.");
    }
}

