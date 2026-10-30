using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Repositories.Emergency;
using SeniorCare.Api.Tests.Services;

namespace SeniorCare.Api.Tests.Integration;

/// <summary>SOS idempotente con hora real y validación de fechas de dosis a través de HTTP.</summary>
[Collection("Integration")]
public sealed class SafetyEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    private static readonly Guid ResidentId = Guid.NewGuid();

    [Fact]
    public async Task Sos_RetriedWithSameId_CreatedOnceKeepsOriginalTime()
    {
        await using var app = new TestApp();
        var clientId = Guid.NewGuid();
        var pressed = DateTimeOffset.UtcNow.AddMinutes(-30);
        var body = new
        {
            residentId = ResidentId,
            residentName = "María López",
            type = "emergency",
            message = "La persona adulta mayor activó el botón SOS.",
            clientRequestId = clientId,
            occurredAt = pressed
        };

        using var first = await app.Client.PostAsJsonAsync("/api/emergencies/alerts", body);
        using var retry = await app.Client.PostAsJsonAsync("/api/emergencies/alerts", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        using var created = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var repeated = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal(
            created.RootElement.GetProperty("id").GetGuid(),
            repeated.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(1, app.Alerts.Count);

        var createdAt = created.RootElement.GetProperty("createdAt").GetDateTimeOffset();
        var receivedAt = created.RootElement.GetProperty("receivedAt").GetDateTimeOffset();
        Assert.True((createdAt - pressed).Duration() < TimeSpan.FromSeconds(1));
        Assert.True(receivedAt > createdAt.AddMinutes(25));
        Assert.True(created.RootElement.GetProperty("delayed").GetBoolean());
        Assert.Equal(clientId, created.RootElement.GetProperty("clientRequestId").GetGuid());
    }

    [Fact]
    public async Task Sos_ClientIdOfAnotherResident_ReturnsConflictWithoutData()
    {
        await using var app = new TestApp();
        var clientId = Guid.NewGuid();

        using var mine = await app.Client.PostAsJsonAsync("/api/emergencies/alerts", new
        {
            residentId = ResidentId,
            residentName = "María López",
            type = "emergency",
            message = "",
            clientRequestId = clientId
        });

        app.UseToken(app.CaregiverToken);
        var otherResident = Guid.NewGuid();
        using var reused = await app.Client.PostAsJsonAsync("/api/emergencies/alerts", new
        {
            residentId = otherResident,
            residentName = "Otra persona",
            type = "emergency",
            message = "",
            clientRequestId = clientId
        });

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var text = await reused.Content.ReadAsStringAsync();
        Assert.DoesNotContain("María", text);
        Assert.Equal(1, app.Alerts.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(26_000)]
    [InlineData(-2)]
    [InlineData(-2_400)]
    public async Task ConfirmDose_OutsideAllowedDates_ReturnsBadRequest(int offsetDays)
    {
        await using var app = new TestApp();
        var date = DateOnly.FromDateTime(DateTime.Now).AddDays(offsetDays);

        using var response = await app.Client.PostAsync(
            $"/api/care/residents/{ResidentId}/medications/{Guid.NewGuid()}/confirm?date={date:yyyy-MM-dd}",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("message", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ConfirmDose_TodayOrYesterday_ReachesScheduleLookup(int? offsetDays)
    {
        await using var app = new TestApp();
        var query = offsetDays is { } days
            ? $"?date={DateOnly.FromDateTime(DateTime.Now).AddDays(days):yyyy-MM-dd}"
            : "";

        // El repositorio falso no tiene horarios: 404 demuestra que la fecha fue aceptada.
        using var response = await app.Client.PostAsync(
            $"/api/care/residents/{ResidentId}/medications/{Guid.NewGuid()}/confirm{query}",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmDose_OtherResident_StillForbidden()
    {
        await using var app = new TestApp();
        var today = DateOnly.FromDateTime(DateTime.Now);

        using var response = await app.Client.PostAsync(
            $"/api/care/residents/{Guid.NewGuid()}/medications/{Guid.NewGuid()}/confirm?date={today:yyyy-MM-dd}",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly string? _previousSecret;
        private readonly WebApplicationFactory<Program> _factory;

        public TestApp()
        {
            _previousSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
            Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<ICareRepository>();
                        services.AddSingleton<ICareRepository>(
                            new CareEndpointTests.FakeCareRepository(Array.Empty<Resident>()));
                        services.RemoveAll<IEmergencyRepository>();
                        services.AddSingleton<IEmergencyRepository>(Alerts);
                    });
                });

            Client = _factory.CreateClient();

            var tokens = new TokenService(new JwtOptions
            {
                Issuer = "SeniorCare.Identity",
                Audience = "SeniorCare.Platform",
                Secret = JwtSecret,
                ExpirationMinutes = 60
            });

            ResidentToken = tokens.Create(
                new UserAccount(Guid.NewGuid(), "maria", "María López", "hash",
                    SeniorCareRoles.Resident, ResidentId, true, DateTimeOffset.UtcNow, null),
                "/panel/adulto-mayor/").AccessToken;

            CaregiverToken = tokens.Create(
                new UserAccount(Guid.NewGuid(), "cuidador", "Cuidador", "hash",
                    SeniorCareRoles.Caregiver, null, true, DateTimeOffset.UtcNow, null),
                "/panel/cuidador/").AccessToken;

            UseToken(ResidentToken);
        }

        public SafetyPolicyTests.IdempotentEmergencyRepository Alerts { get; } = new();
        public HttpClient Client { get; }
        public string ResidentToken { get; }
        public string CaregiverToken { get; }

        public void UseToken(string token) =>
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            Environment.SetEnvironmentVariable("JWT_SECRET", _previousSecret);
        }
    }
}
