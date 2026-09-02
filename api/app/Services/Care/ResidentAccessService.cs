using System.Security.Claims;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Services.Care;

public sealed class ResidentAccessService
{
    public bool CanRead(ClaimsPrincipal principal, Guid residentId) =>
        principal.CanAccessResident(residentId);

    public bool CanManageCare(ClaimsPrincipal principal, Guid residentId) =>
        principal.IsCareTeam() && principal.CanAccessResident(residentId);

    public bool CanConfirmDose(ClaimsPrincipal principal, Guid residentId) =>
        principal.CanAccessResident(residentId)
        && (principal.IsCareTeam()
            || principal.IsInRole(SeniorCareRoles.Resident));
}
