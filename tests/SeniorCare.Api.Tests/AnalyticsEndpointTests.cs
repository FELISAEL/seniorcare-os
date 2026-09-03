using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Analytics;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Analytics;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class AnalyticsEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    [Fact]
    public async Task Recent_WithAdminRole_ReturnsMetrics()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var metric = new DailyMetric(
                new DateOnly(2026, 9, 2),
                10,
                8,
                2,
                1,
                1,
                DateTimeOffset.UtcNow);

            var repository =
                new FakeAnalyticsRepository([metric]);

            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<IAnalyticsRepository>();

                            services.AddSingleton<IAnalyticsRepository>(
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
                "/api/analytics/recent?days=7");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var metrics = await response.Content
                .ReadFromJsonAsync<List<DailyMetric>>();

            Assert.NotNull(metrics);
            Assert.Single(metrics!);

            Assert.Equal(
                new DateOnly(2026, 9, 2),
                metrics[0].MetricDate);

            Assert.Equal(10, metrics[0].ScheduledDoses);
            Assert.Equal(8, metrics[0].TakenDoses);
            Assert.Equal(80m, metrics[0].AdherencePercentage);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    private sealed class FakeAnalyticsRepository(
        IReadOnlyList<DailyMetric> metrics) : IAnalyticsRepository
    {
        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<DailyMetric?> FindAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DailyMetric?>(
                metrics.FirstOrDefault(metric =>
                    metric.MetricDate == date));

        public Task<IReadOnlyList<DailyMetric>> ListRecentAsync(
            int days,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(metrics);
    }
}
