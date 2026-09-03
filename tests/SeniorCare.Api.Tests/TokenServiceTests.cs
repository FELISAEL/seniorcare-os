using System.IdentityModel.Tokens.Jwt;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Identity;

namespace SeniorCare.Api.Tests.Core.Auth;

public sealed class TokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "SeniorCare.Tests",
        Audience = "SeniorCare.Tests.Client",
        Secret = "SeniorCare-Test-Secret-Key-2026-At-Least-32-Bytes",
        ExpirationMinutes = 60
    };

    [Fact]
    public void Create_WithResidentUser_GeneratesExpectedClaims()
    {
        var residentId = Guid.NewGuid();
        var user = new UserAccount(
            Guid.NewGuid(),
            "maria",
            "María López",
            "hash",
            "resident",
            residentId,
            true,
            DateTimeOffset.UtcNow,
            null);

        var service = new TokenService(Options);

        var result = service.Create(user, "/panel/residente/");
        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(
            user.Username,
            token.Claims.Single(
                claim => claim.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal(
            user.DisplayName,
            token.Claims.Single(claim => claim.Type == "name").Value);
        Assert.Equal(
            user.Role,
            token.Claims.Single(claim => claim.Type == "role").Value);
        Assert.Equal(
            residentId.ToString(),
            token.Claims.Single(claim => claim.Type == "resident_id").Value);
        Assert.Equal("/panel/residente/", result.PanelPath);
    }

    [Fact]
    public void Create_WithUserWithoutResident_DoesNotAddResidentClaim()
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            "admin",
            "Administrador SeniorCare",
            "hash",
            "admin",
            null,
            true,
            DateTimeOffset.UtcNow,
            null);

        var service = new TokenService(Options);

        var result = service.Create(user, "/panel/administracion/");
        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        Assert.DoesNotContain(
            token.Claims,
            claim => claim.Type == "resident_id");
    }
}
