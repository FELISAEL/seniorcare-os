namespace SeniorCare.Api.App.Core.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } =
        "SeniorCare.Identity";

    public string Audience { get; init; } =
        "SeniorCare.Platform";

    public string Secret { get; init; } =
        string.Empty;

    public int ExpirationMinutes { get; init; } =
        480;
}