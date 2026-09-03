using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Care;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class CareEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    [Fact]
    public async Task Residents_WithAdminRole_ReturnsResidents()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var resident = new Resident(
                Guid.NewGuid(),
                "maria",
                "María López",
                new DateOnly(1948, 4, 12),
                "Ana López",
                "+502 5555-0101",
                DateTimeOffset.UtcNow);

            var repository = new FakeCareRepository([resident]);

            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<ICareRepository>();

                            services.AddSingleton<ICareRepository>(
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
                "/api/care/residents");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            var residents = json.RootElement;

            Assert.Equal(
                JsonValueKind.Array,
                residents.ValueKind);

            Assert.Single(
                residents.EnumerateArray());

            Assert.Equal(
                "maria",
                residents[0]
                    .GetProperty("username")
                    .GetString());

            Assert.Equal(
                "María López",
                residents[0]
                    .GetProperty("fullName")
                    .GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    private sealed class FakeCareRepository(
        IReadOnlyList<Resident> residents) : ICareRepository
    {
        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Resident>> ListResidentsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(residents);

        public Task<Resident?> FindResidentByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Resident?>(null);

        public Task<Resident?> FindResidentByIdAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Resident?>(null);

        public Task<Resident> CreateResidentAsync(
            CreateResidentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MedicationScheduleView>> GetTodayAsync(
            Guid residentId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicationScheduleView>>(
                Array.Empty<MedicationScheduleView>());

        public Task<MedicationScheduleView> CreateMedicationAsync(
            Guid residentId,
            CreateMedicationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DoseConfirmation?> ConfirmDoseAsync(
            Guid residentId,
            Guid scheduleId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DoseConfirmation?>(null);
    }
}
