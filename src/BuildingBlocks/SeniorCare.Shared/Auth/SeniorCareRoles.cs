namespace SeniorCare.Shared.Auth;

public static class SeniorCareRoles
{
    public const string Admin = "admin";
    public const string Caregiver = "caregiver";
    public const string Resident = "resident";
    public const string Family = "family";

    public static readonly string[] All =
    [
        Admin,
        Caregiver,
        Resident,
        Family
    ];

    public static bool IsSupported(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && All.Contains(role.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}
