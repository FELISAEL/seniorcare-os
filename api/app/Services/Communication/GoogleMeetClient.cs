using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SeniorCare.Api.App.Services.Communication;

/// <summary>
/// Credenciales OAuth para la API de Google Meet. Se leen del .env:
/// GOOGLE_MEET_ENABLED, GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET y
/// GOOGLE_REFRESH_TOKEN. Nunca van en el repositorio.
/// </summary>
public sealed record GoogleMeetOptions(
    bool Enabled,
    string ClientId,
    string ClientSecret,
    string RefreshToken)
{
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RefreshToken);

    public static GoogleMeetOptions FromConfiguration(IConfiguration configuration)
    {
        static string Read(IConfiguration c, string env, string section) =>
            (c[env] ?? c[section] ?? string.Empty).Trim();

        return new GoogleMeetOptions(
            bool.TryParse(Read(configuration, "GOOGLE_MEET_ENABLED", "GoogleMeet:Enabled"), out var enabled)
                && enabled,
            Read(configuration, "GOOGLE_CLIENT_ID", "GoogleMeet:ClientId"),
            Read(configuration, "GOOGLE_CLIENT_SECRET", "GoogleMeet:ClientSecret"),
            Read(configuration, "GOOGLE_REFRESH_TOKEN", "GoogleMeet:RefreshToken"));
    }
}

public sealed record MeetSpace(string Name, string MeetingUri, string MeetingCode);

public sealed class GoogleMeetClient(HttpClient http, GoogleMeetOptions options)
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public bool IsConfigured => options.IsConfigured;

    /// <summary>Crea una reunión nueva (cualquiera con el enlace puede entrar).</summary>
    public async Task<MeetSpace> CreateSpaceAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://meet.googleapis.com/v2/spaces");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Content = new StringContent(
            "{\"config\":{\"accessType\":\"OPEN\"}}", Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Meet respondió {(int)response.StatusCode}: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        return new MeetSpace(
            root.GetProperty("name").GetString() ?? string.Empty,
            root.GetProperty("meetingUri").GetString() ?? string.Empty,
            root.TryGetProperty("meetingCode", out var code) ? code.GetString() ?? string.Empty : string.Empty);
    }

    /// <summary>Termina la conferencia activa (si no hay ninguna, Google responde error y se ignora).</summary>
    public async Task EndActiveConferenceAsync(
        string spaceName,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://meet.googleapis.com/v2/{spaceName}:endActiveConference");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        _ = response.StatusCode;
    }

    /// <summary>
    /// Indica si hay al menos una persona conectada ahora mismo en la reunión.
    /// Requiere el permiso meetings.space.readonly además de meetings.space.created.
    /// </summary>
    public async Task<bool> HasActiveParticipantsAsync(
        string spaceName,
        CancellationToken cancellationToken = default)
    {
        var recordsFilter = Uri.EscapeDataString($"space.name = \"{spaceName}\" AND end_time IS NULL");
        var records = await GetJsonAsync(
            $"https://meet.googleapis.com/v2/conferenceRecords?filter={recordsFilter}&pageSize=1",
            cancellationToken);

        if (!records.RootElement.TryGetProperty("conferenceRecords", out var list)
            || list.GetArrayLength() == 0)
        {
            return false;
        }

        var recordName = list[0].GetProperty("name").GetString();
        var participantsFilter = Uri.EscapeDataString("latest_end_time IS NULL");
        var participants = await GetJsonAsync(
            $"https://meet.googleapis.com/v2/{recordName}/participants?filter={participantsFilter}&pageSize=5",
            cancellationToken);

        return participants.RootElement.TryGetProperty("participants", out var people)
            && people.GetArrayLength() > 0;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Meet respondió {(int)response.StatusCode}: {body}");
        }

        return JsonDocument.Parse(body);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt)
            {
                return _accessToken;
            }

            using var response = await http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["refresh_token"] = options.RefreshToken,
                    ["grant_type"] = "refresh_token"
                }),
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google rechazó las credenciales ({(int)response.StatusCode}): {body}");
            }

            using var json = JsonDocument.Parse(body);
            _accessToken = json.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Google no devolvió access_token.");

            var seconds = json.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 3600;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, seconds - 120));

            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
