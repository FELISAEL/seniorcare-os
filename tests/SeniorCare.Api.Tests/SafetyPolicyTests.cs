using Microsoft.Extensions.Configuration;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Emergency;
using SeniorCare.Api.App.Repositories.Emergency;
using SeniorCare.Api.App.Services.Care;
using SeniorCare.Api.App.Services.Emergency;

namespace SeniorCare.Api.Tests.Services;

/// <summary>Reglas de seguridad de la Etapa 11.1: hora real del SOS, fechas de dosis y secretos.</summary>
public sealed class SafetyPolicyTests
{
    private static readonly DateTimeOffset ReceivedAt =
        new(2026, 10, 30, 18, 0, 0, TimeSpan.Zero);

    // ---------- Hora real del SOS ----------

    [Fact]
    public void ResolveOccurredAt_WithoutReportedTime_UsesArrival()
    {
        Assert.Equal(ReceivedAt, AlertService.ResolveOccurredAt(null, ReceivedAt));
    }

    [Fact]
    public void ResolveOccurredAt_QueuedForHours_KeepsOriginalTime()
    {
        var pressed = ReceivedAt.AddHours(-5).AddMinutes(-13);

        Assert.Equal(pressed, AlertService.ResolveOccurredAt(pressed, ReceivedAt));
    }

    [Fact]
    public void ResolveOccurredAt_SmallClockSkewAhead_NeverInTheFuture()
    {
        var reported = ReceivedAt.AddSeconds(40);

        Assert.Equal(ReceivedAt, AlertService.ResolveOccurredAt(reported, ReceivedAt));
    }

    [Theory]
    [InlineData(10)]       // reloj del dispositivo muy adelantado
    [InlineData(-73 * 60)] // más viejo que la ventana de cola
    [InlineData(-60 * 24 * 365 * 6)]
    public void ResolveOccurredAt_ImplausibleTime_UsesArrival(int minutes)
    {
        var reported = ReceivedAt.AddMinutes(minutes);

        Assert.Equal(ReceivedAt, AlertService.ResolveOccurredAt(reported, ReceivedAt));
    }

    [Fact]
    public async Task CreateAsync_StoresOriginalTimeArrivalAndClientId()
    {
        var repository = new IdempotentEmergencyRepository();
        var service = new AlertService(repository, null, new FixedClock(ReceivedAt));
        var clientId = Guid.NewGuid();
        var pressed = ReceivedAt.AddMinutes(-20);

        var result = await service.CreateAsync(new CreateAlertRequest(
            Guid.NewGuid(), "María López", "emergency", "", clientId, pressed));

        Assert.True(result.Created);
        Assert.Equal(pressed, result.Alert.CreatedAt);
        Assert.Equal(ReceivedAt, result.Alert.ReceivedAt);
        Assert.Equal(clientId, result.Alert.ClientRequestId);
        Assert.True(result.Alert.Delayed);
        Assert.Equal(pressed, repository.LastReportedAt);
    }

    [Fact]
    public async Task CreateAsync_SameClientId_DoesNotDuplicate()
    {
        var repository = new IdempotentEmergencyRepository();
        var service = new AlertService(repository, null, new FixedClock(ReceivedAt));
        var request = new CreateAlertRequest(
            Guid.NewGuid(), "María López", "emergency", "", Guid.NewGuid(), ReceivedAt);

        var first = await service.CreateAsync(request);
        var retry = await service.CreateAsync(request);

        Assert.True(first.Created);
        Assert.False(retry.Created);
        Assert.Equal(first.Alert.Id, retry.Alert.Id);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task CreateAsync_WithoutClientId_KeepsPreviousBehavior()
    {
        var repository = new IdempotentEmergencyRepository();
        var service = new AlertService(repository, null, new FixedClock(ReceivedAt));
        var request = new CreateAlertRequest(Guid.NewGuid(), "María López", "assistance", "");

        await service.CreateAsync(request);
        await service.CreateAsync(request);

        Assert.Equal(2, repository.Count);
    }

    // ---------- Fechas de dosis ----------

    private static readonly DateOnly Today = new(2026, 10, 30);

    [Fact]
    public void DosePolicy_AcceptsTodayAndYesterdayByDefault()
    {
        Assert.Null(DoseConfirmationPolicy.Validate(Today, Today, 1));
        Assert.Null(DoseConfirmationPolicy.Validate(Today.AddDays(-1), Today, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(26_000)] // año 2099
    public void DosePolicy_RejectsFutureDates(int daysAhead)
    {
        Assert.NotNull(DoseConfirmationPolicy.Validate(Today.AddDays(daysAhead), Today, 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(2_400)] // año 2020
    public void DosePolicy_RejectsDatesOutsideLateWindow(int daysBack)
    {
        Assert.NotNull(DoseConfirmationPolicy.Validate(Today.AddDays(-daysBack), Today, 1));
    }

    [Fact]
    public void DosePolicy_WindowZero_OnlyToday()
    {
        Assert.Null(DoseConfirmationPolicy.Validate(Today, Today, 0));
        Assert.NotNull(DoseConfirmationPolicy.Validate(Today.AddDays(-1), Today, 0));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("0", 0)]
    [InlineData("3", 3)]
    [InlineData("90", 7)] // tope: nunca una ventana abierta
    [InlineData("-4", 0)]
    [InlineData("abc", 1)]
    public void DosePolicy_ReadsConfiguredWindowWithLimits(string? configured, int expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DoseConfirmationPolicy.SettingName] = configured
            })
            .Build();

        Assert.Equal(expected, DoseConfirmationPolicy.ReadMaxDaysLate(configuration));
    }

    // ---------- Secretos ----------

    [Theory]
    [InlineData("CAMBIAR_CONTRASENA")]
    [InlineData("CAMBIAR_CLAVE_JWT_MINIMO_32_CARACTERES")]
    [InlineData("cambiar_esto_por_algo")]
    [InlineData("CHANGE_ME")]
    public void Guard_RejectsPlaceholders(string value)
    {
        Assert.True(InsecureSecretGuard.IsInsecure(value));
    }

    [Fact]
    public void Guard_AcceptsGeneratedSecret()
    {
        var generated = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

        Assert.False(InsecureSecretGuard.IsInsecure(generated));
        Assert.False(InsecureSecretGuard.IsInsecure(null));
    }

    [Fact]
    public void Guard_RejectsEverySecretInEnvExample()
    {
        var example = Path.Combine(RepositoryRoot(), ".env.example");
        var secrets = File.ReadAllLines(example)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2
                && (parts[0].Trim() == "POSTGRES_PASSWORD"
                    || InsecureSecretGuard.SecretSettings.Contains(parts[0].Trim())))
            .ToList();

        Assert.Equal(6, secrets.Count);
        Assert.All(secrets, parts => Assert.True(
            InsecureSecretGuard.IsInsecure(parts[1]),
            $"{parts[0]} de .env.example debería rechazarse."));
    }

    [Fact]
    public void Guard_ReportsNamesNeverValues()
    {
        const string placeholder = "CAMBIAR_CONTRASENA";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "CAMBIAR_CLAVE_JWT_MINIMO_32_CARACTERES",
                ["SEED_ADMIN_PASSWORD"] = placeholder,
                ["SEED_FAMILY_PASSWORD"] = "Valor-Seguro-Generado-Para-La-Prueba-123",
                ["ConnectionStrings:CareDatabase"] =
                    $"Host=postgres;Database=care_db;Username=seniorcare;Password={placeholder}"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => InsecureSecretGuard.EnsureSafe(configuration));

        Assert.Contains("JWT_SECRET", error.Message);
        Assert.Contains("SEED_ADMIN_PASSWORD", error.Message);
        Assert.Contains("CareDatabase", error.Message);
        Assert.DoesNotContain("SEED_FAMILY_PASSWORD", error.Message);
        Assert.DoesNotContain(placeholder, error.Message);
    }

    [Fact]
    public void Guard_AllowsSafeConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes",
                ["ConnectionStrings:CareDatabase"] =
                    "Host=postgres;Database=care_db;Username=seniorcare;Password=Otro-Valor-Seguro-456"
            })
            .Build();

        InsecureSecretGuard.EnsureSafe(configuration);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SeniorCare.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Imita la restricción única de client_request_id de la base real.</summary>
    internal sealed class IdempotentEmergencyRepository : IEmergencyRepository
    {
        private readonly List<Alert> _alerts = [];

        public int Count => _alerts.Count;
        public DateTimeOffset? LastReportedAt { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AlertCreationResult> CreateAsync(
            Alert alert,
            DateTimeOffset? reportedAt,
            CancellationToken cancellationToken = default)
        {
            lock (_alerts)
            {
                LastReportedAt = reportedAt;
                var existing = alert.ClientRequestId is { } id
                    ? _alerts.FirstOrDefault(item => item.ClientRequestId == id)
                    : null;
                if (existing is not null)
                {
                    return Task.FromResult(new AlertCreationResult(existing, false));
                }

                _alerts.Add(alert);
                return Task.FromResult(new AlertCreationResult(alert, true));
            }
        }

        public Task<IReadOnlyList<Alert>> ListActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Alert>>(_alerts.ToList());

        public Task<IReadOnlyList<Alert>> ListForResidentAsync(
            Guid residentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Alert>>(
                _alerts.Where(alert => alert.ResidentId == residentId).ToList());

        public Task<Alert?> UpdateStatusAsync(
            Guid alertId,
            string status,
            string updatedBy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Alert?>(null);
    }
}
