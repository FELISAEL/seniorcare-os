using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Identity;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class RoleAuthorizationEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    [Fact]
    public async Task CommunicationActiveRooms_WithResidentRole_ReturnsForbidden()
    {
        const string variable = "JWT_SECRET";
        var previousValue = Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(variable, JwtSecret);

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                });

            using var client = factory.CreateClient();

            var tokenService = new TokenService(
                new JwtOptions
                {
                    Issuer = "SeniorCare.Identity",
                    Audience = "SeniorCare.Platform",
                    Secret = JwtSecret,
                    ExpirationMinutes = 60
                });

            var resident = new UserAccount(
                Guid.NewGuid(),
                "resident-test",
                "Residente Test",
                "hash",
                SeniorCareRoles.Resident,
                Guid.NewGuid(),
                true,
                DateTimeOffset.UtcNow,
                null);

            var login = tokenService.Create(
                resident,
                "/panel/adulto-mayor/");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    login.AccessToken);

            var response = await client.GetAsync(
                "/api/communication/video-rooms/active");

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }
}

