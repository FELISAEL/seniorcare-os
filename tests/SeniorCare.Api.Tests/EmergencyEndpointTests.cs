using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Emergency;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Emergency;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class EmergencyEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    [Fact]
    public async Task ActiveAlerts_WithAdminRole_ReturnsAlerts()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var alert = new Alert(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "María López",
                "assistance",
                "Necesita asistencia.",
                "active",
                DateTimeOffset.UtcNow,
                null,
                null);

            var repository =
                new FakeEmergencyRepository([alert]);

            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<IEmergencyRepository>();

                            services.AddSingleton<IEmergencyRepository>(
                                repository);
                        });
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

            using var response = await client.GetAsync(
                "/api/emergencies/alerts/active");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            var alerts = json.RootElement;

            Assert.Single(alerts.EnumerateArray());

            Assert.Equal(
                "assistance",
                alerts[0].GetProperty("type").GetString());

            Assert.Equal(
                "active",
                alerts[0].GetProperty("status").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    private sealed class FakeEmergencyRepository(
        IReadOnlyList<Alert> alerts) : IEmergencyRepository
    {
        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Alert> CreateAsync(
            CreateAlertRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Alert>> ListActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(alerts);

        public Task<IReadOnlyList<Alert>> ListForResidentAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(alerts);

        public Task<Alert?> UpdateStatusAsync(
            Guid alertId,
            string status,
            string updatedBy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Alert?>(null);
    }
}
