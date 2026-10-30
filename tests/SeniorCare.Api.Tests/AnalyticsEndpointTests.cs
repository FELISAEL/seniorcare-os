using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
        var metric = new DailyMetric(
            new DateOnly(2026, 9, 2),
            10,
            8,
            2,
            1,
            1,
            DateTimeOffset.UtcNow);

        var (status, body) = await GetRecentAsync([metric]);

        Assert.Equal(HttpStatusCode.OK, status);

        var metrics = JsonSerializer.Deserialize<List<DailyMetric>>(
            body,
            JsonSerializerOptions.Web);

        Assert.NotNull(metrics);
        Assert.Single(metrics!);

        Assert.Equal(
            new DateOnly(2026, 9, 2),
            metrics[0].MetricDate);

        Assert.Equal(10, metrics[0].ScheduledDoses);
        Assert.Equal(8, metrics[0].TakenDoses);
        Assert.Equal(80m, metrics[0].AdherencePercentage);
        Assert.Equal("complete", metrics[0].DataQuality);
    }

    [Fact]
    public async Task Recent_WithPartialDay_ReturnsNullPlanAndAdherence()
    {
        var metric = new DailyMetric(
            new DateOnly(2026, 10, 29),
            null,
            0,
            1,
            1,
            0,
            DateTimeOffset.UtcNow,
            "partial");

        var (status, body) = await GetRecentAsync([metric]);

        Assert.Equal(HttpStatusCode.OK, status);

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement[0];

        Assert.Equal(JsonValueKind.Null, item.GetProperty("scheduledDoses").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("adherencePercentage").ValueKind);
        Assert.Equal(0, item.GetProperty("takenDoses").GetInt32());
        Assert.Equal(1, item.GetProperty("emergencyAlerts").GetInt32());
        Assert.Equal("partial", item.GetProperty("dataQuality").GetString());
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetRecentAsync(
        IReadOnlyList<DailyMetric> metrics)
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var repository =
                new FakeAnalyticsRepository(metrics);

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

            return (response.StatusCode, await response.Content.ReadAsStringAsync());
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
