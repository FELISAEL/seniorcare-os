using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Communication;
using SeniorCare.Api.App.Models.Identity;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class CommunicationEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    [Fact]
    public async Task CreateRoom_ThenListActive_WithAdminRole_ReturnsRoom()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            await using var factory =
                new WebApplicationFactory<Program>()
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

            var admin = new UserAccount(
                Guid.NewGuid(),
                "admin",
                "Administrador SeniorCare",
                "hash",
                SeniorCareRoles.Admin,
                null,
                true,
                DateTimeOffset.UtcNow,
                null);

            var login = tokenService.Create(
                admin,
                "/panel/administracion/");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    login.AccessToken);

            var residentId = Guid.NewGuid();

            using var createResponse =
                await client.PostAsJsonAsync(
                    "/api/communication/video-rooms",
                    new CreateVideoRoomRequest(
                        residentId,
                        "María López"));

            Assert.Equal(
                HttpStatusCode.OK,
                createResponse.StatusCode);

            var room =
                await createResponse.Content
                    .ReadFromJsonAsync<VideoRoom>();

            Assert.NotNull(room);
            Assert.Equal(residentId, room!.ResidentId);
            Assert.Equal("active", room.Status);

            using var listResponse = await client.GetAsync(
                "/api/communication/video-rooms/active");

            Assert.Equal(
                HttpStatusCode.OK,
                listResponse.StatusCode);

            var activeRooms =
                await listResponse.Content
                    .ReadFromJsonAsync<List<VideoRoom>>();

            Assert.NotNull(activeRooms);
            Assert.Contains(
                activeRooms!,
                item => item.Id == room.Id);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }
}
