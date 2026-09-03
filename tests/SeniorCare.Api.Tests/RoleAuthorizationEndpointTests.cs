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
        var response = await SendRequestAsync(
            SeniorCareRoles.Resident,
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommunicationActiveRooms_WithAdminRole_ReturnsOk()
    {
        var response = await SendRequestAsync(
            SeniorCareRoles.Admin,
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        string role,
        Guid? residentId)
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

            var user = new UserAccount(
                Guid.NewGuid(),
                "integration-test",
                "Usuario Integration Test",
                "hash",
                role,
                residentId,
                true,
                DateTimeOffset.UtcNow,
                null);

            var login = tokenService.Create(user, "/test/");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    login.AccessToken);

            return await client.GetAsync(
                "/api/communication/video-rooms/active");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }
}
