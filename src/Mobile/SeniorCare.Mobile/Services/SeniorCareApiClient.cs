using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeniorCare.Mobile.Models;

namespace SeniorCare.Mobile.Services;

public sealed class SeniorCareApiClient
{
    private const string TokenPreference = "seniorcare_access_token";
    private const string UserPreference = "seniorcare_user";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SeniorCareApiClient()
    {
        var baseUrl = DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:8088"
            : "http://localhost:8088";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
        RestoreSession();
    }

    public PublicUser? CurrentUser { get; private set; }
    public bool IsAuthenticated =>
        _httpClient.DefaultRequestHeaders.Authorization is not null;

    public async Task<PublicUser> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/identity/login",
            new LoginRequest(username, password),
            _jsonOptions,
            cancellationToken);
        var result = await ReadAsync<LoginResponse>(response, cancellationToken);

        if (result.User.Role is not ("admin" or "caregiver"))
        {
            throw new InvalidOperationException(
                "Este usuario no tiene acceso a la aplicación de cuidado.");
        }

        SetSession(result.AccessToken, result.User);
        return result.User;
    }

    public async Task<IReadOnlyList<AlertItem>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            "/api/emergencies/alerts/active",
            cancellationToken);
        return await ReadAsync<List<AlertItem>>(response, cancellationToken);
    }

    public async Task UpdateAlertAsync(
        Guid alertId,
        string status,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/emergencies/alerts/{alertId}")
        {
            Content = JsonContent.Create(
                new { status },
                options: _jsonOptions)
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<VideoRoom>> GetVideoRoomsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            "/api/communication/video-rooms/active",
            cancellationToken);
        return await ReadAsync<List<VideoRoom>>(response, cancellationToken);
    }

    public void Logout()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        CurrentUser = null;
        Preferences.Default.Remove(TokenPreference);
        Preferences.Default.Remove(UserPreference);
    }

    private void SetSession(string token, PublicUser user)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        CurrentUser = user;
        Preferences.Default.Set(TokenPreference, token);
        Preferences.Default.Set(
            UserPreference,
            JsonSerializer.Serialize(user, _jsonOptions));
    }

    private void RestoreSession()
    {
        var token = Preferences.Default.Get(TokenPreference, string.Empty);
        var userJson = Preferences.Default.Get(UserPreference, string.Empty);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userJson))
        {
            return;
        }

        try
        {
            CurrentUser = JsonSerializer.Deserialize<PublicUser>(
                userJson,
                _jsonOptions);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        catch (JsonException)
        {
            Logout();
        }
    }

    private async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(
            _jsonOptions,
            cancellationToken);
        return result
            ?? throw new InvalidOperationException(
                "El servidor respondió sin información.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = "No se pudo completar la operación.";
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
            message = payload?.Message ?? message;
        }
        catch (JsonException)
        {
            // Conserva el mensaje general cuando el servidor no retorna JSON.
        }

        throw new InvalidOperationException(message);
    }

    private sealed record ApiError(string Message);
}
