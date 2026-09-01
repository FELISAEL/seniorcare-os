namespace SeniorCare.Shared.Auth;

public static class SeniorCarePolicies
{
    public const string AdminOnly = "SeniorCare.AdminOnly";
    public const string CareTeam = "SeniorCare.CareTeam";
    public const string ResidentOnly = "SeniorCare.ResidentOnly";
    public const string FamilyOnly = "SeniorCare.FamilyOnly";
    public const string ResidentOrCareTeam = "SeniorCare.ResidentOrCareTeam";
    public const string FamilyOrCareTeam = "SeniorCare.FamilyOrCareTeam";
    public const string ResidentFamilyOrCareTeam = "SeniorCare.ResidentFamilyOrCareTeam";
}
