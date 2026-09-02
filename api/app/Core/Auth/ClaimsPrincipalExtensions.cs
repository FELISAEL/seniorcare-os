using System.Security.Claims;

namespace SeniorCare.Api.App.Core.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var value)
            ? value
            : null;

    public static string? GetUsername(this ClaimsPrincipal principal) =>
        principal.FindFirst("unique_name")?.Value;

    public static string? GetRole(this ClaimsPrincipal principal) =>
        principal.FindFirst("role")?.Value;

    public static Guid? GetResidentId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("resident_id")?.Value, out var value)
            ? value
            : null;

    public static bool IsCareTeam(this ClaimsPrincipal principal) =>
        principal.IsInRole(SeniorCareRoles.Admin)
        || principal.IsInRole(SeniorCareRoles.Caregiver);

    public static bool CanAccessResident(
        this ClaimsPrincipal principal,
        Guid residentId)
    {
        if (principal.IsCareTeam())
        {
            return true;
        }

        if (!principal.IsInRole(SeniorCareRoles.Resident)
            && !principal.IsInRole(SeniorCareRoles.Family))
        {
            return false;
        }

        return principal.GetResidentId() == residentId;
    }
}
