using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SeniorCare.Api.App.Models.Identity;

namespace SeniorCare.Api.App.Core.Auth;

public sealed class TokenService(JwtOptions options)
{
    public LoginResponse Create(
        UserAccount user,
        string panelPath)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt =
            now.AddMinutes(options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new(
                JwtRegisteredClaimNames.UniqueName,
                user.Username),

            new("name", user.DisplayName),
            new("role", user.Role)
        };

        if (user.ResidentId.HasValue)
        {
            claims.Add(
                new Claim(
                    "resident_id",
                    user.ResidentId.Value.ToString()));
        }

        var signingKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.Secret));

        var credentials =
            new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expiresAt.UtcDateTime,
                signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler()
                .WriteToken(token),
            expiresAt,
            ToPublic(user),
            panelPath);
    }

    public static PublicUser ToPublic(
        UserAccount user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.ResidentId,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
}