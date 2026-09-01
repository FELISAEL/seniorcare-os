using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SeniorCare.Shared.Auth;

public static class AuthExtensions
{
    public static IServiceCollection AddSeniorCareAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = ReadOptions(configuration);
        services.AddSingleton(options);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(
                SeniorCarePolicies.AdminOnly,
                policy => policy.RequireRole(SeniorCareRoles.Admin));

            authorization.AddPolicy(
                SeniorCarePolicies.CareTeam,
                policy => policy.RequireRole(
                    SeniorCareRoles.Admin,
                    SeniorCareRoles.Caregiver));

            authorization.AddPolicy(
                SeniorCarePolicies.ResidentOnly,
                policy => policy.RequireRole(SeniorCareRoles.Resident));

            authorization.AddPolicy(
                SeniorCarePolicies.FamilyOnly,
                policy => policy.RequireRole(SeniorCareRoles.Family));

            authorization.AddPolicy(
                SeniorCarePolicies.ResidentOrCareTeam,
                policy => policy.RequireRole(
                    SeniorCareRoles.Admin,
                    SeniorCareRoles.Caregiver,
                    SeniorCareRoles.Resident));

            authorization.AddPolicy(
                SeniorCarePolicies.FamilyOrCareTeam,
                policy => policy.RequireRole(
                    SeniorCareRoles.Admin,
                    SeniorCareRoles.Caregiver,
                    SeniorCareRoles.Family));

            authorization.AddPolicy(
                SeniorCarePolicies.ResidentFamilyOrCareTeam,
                policy => policy.RequireRole(
                    SeniorCareRoles.Admin,
                    SeniorCareRoles.Caregiver,
                    SeniorCareRoles.Resident,
                    SeniorCareRoles.Family));
        });

        return services;
    }

    public static JwtOptions ReadOptions(IConfiguration configuration)
    {
        var configured = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        var options = new JwtOptions
        {
            Issuer = configuration["JWT_ISSUER"]
                ?? configured?.Issuer
                ?? "SeniorCare.Identity",
            Audience = configuration["JWT_AUDIENCE"]
                ?? configured?.Audience
                ?? "SeniorCare.Platform",
            Secret = configuration["JWT_SECRET"]
                ?? configured?.Secret
                ?? "SeniorCare-Development-Key-Change-Before-Deploying-2026-At-Least-32-Bytes",
            ExpirationMinutes = configured?.ExpirationMinutes ?? 480
        };

        if (Encoding.UTF8.GetByteCount(options.Secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT_SECRET debe contener al menos 32 bytes para SeniorCare OS.");
        }

        return options;
    }
}
