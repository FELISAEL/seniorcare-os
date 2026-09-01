namespace SeniorCare.Shared.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "SeniorCare.Identity";
    public string Audience { get; init; } = "SeniorCare.Platform";
    public string Secret { get; init; } =
        "SeniorCare-Development-Key-Change-Before-Deploying-2026-At-Least-32-Bytes";
    public int ExpirationMinutes { get; init; } = 480;
}

