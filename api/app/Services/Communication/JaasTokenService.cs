using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SeniorCare.Api.App.Services.Communication;

/// <summary>
/// Jitsi alojado por 8x8 (JaaS). Firma con RS256 el token (JWT) que permite
/// entrar a una sala como moderador, así nadie queda esperando a que "llegue
/// un moderador". Variables: JAAS_KEY_ID (kid completo "vpaas-magic-cookie-…/xxxx"),
/// JAAS_APP_ID (opcional, se deduce del kid) y la llave privada en
/// JAAS_PRIVATE_KEY (PEM, o PEM en base64) o JAAS_PRIVATE_KEY_FILE.
/// </summary>
public sealed class JaasTokenService
{
    private const string Domain = "https://8x8.vc";

    private readonly string _appId = string.Empty;
    private readonly string _kid = string.Empty;
    private readonly RSA? _rsa;
    private readonly object _signLock = new();

    public JaasTokenService(string? appId, string? keyId, string? privateKey)
    {
        keyId = keyId?.Trim();
        appId = appId?.Trim();

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        var slash = keyId.IndexOf('/');
        if (slash > 0)
        {
            _kid = keyId;
            _appId = string.IsNullOrWhiteSpace(appId) ? keyId[..slash] : appId;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            _appId = appId;
            _kid = $"{appId}/{keyId}";
        }
        else
        {
            return;
        }

        var pem = ResolvePem(privateKey);
        if (pem is null)
        {
            return;
        }

        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _rsa = rsa;
        }
        catch (Exception)
        {
            _rsa = null;
        }
    }

    public bool IsConfigured => _rsa is not null;

    public string AppId => _appId;

    public static JaasTokenService FromConfiguration(IConfiguration configuration)
    {
        string? Read(string key) =>
            configuration[key] ?? Environment.GetEnvironmentVariable(key);

        var inline = Read("JAAS_PRIVATE_KEY");
        var file = Read("JAAS_PRIVATE_KEY_FILE");
        if (string.IsNullOrWhiteSpace(inline)
            && !string.IsNullOrWhiteSpace(file)
            && File.Exists(file))
        {
            inline = File.ReadAllText(file);
        }

        return new JaasTokenService(Read("JAAS_APP_ID"), Read("JAAS_KEY_ID"), inline);
    }

    /// <summary>Dirección de la sala sin token.</summary>
    public string RoomUrl(string roomCode) => $"{Domain}/{_appId}/{roomCode}";

    /// <summary>Dirección de la sala con un token personal que abre la llamada directo.</summary>
    public string BuildJoinUrl(
        string roomCode,
        string displayName,
        bool moderator,
        TimeSpan lifetime,
        DateTimeOffset? now = null)
    {
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        return $"{RoomUrl(roomCode)}?jwt={BuildToken(roomCode, displayName, moderator, lifetime, issuedAt)}";
    }

    public string BuildToken(
        string roomCode,
        string displayName,
        bool moderator,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        if (_rsa is null)
        {
            throw new InvalidOperationException("JaaS no está configurado.");
        }

        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["kid"] = _kid,
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            ["aud"] = "jitsi",
            ["iss"] = "chat",
            ["sub"] = _appId,
            ["room"] = roomCode,
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.Add(lifetime).ToUnixTimeSeconds(),
            ["context"] = new Dictionary<string, object>
            {
                ["user"] = new Dictionary<string, object>
                {
                    ["id"] = Guid.NewGuid().ToString("N"),
                    ["name"] = string.IsNullOrWhiteSpace(displayName) ? "SeniorCare" : displayName.Trim(),
                    ["moderator"] = moderator ? "true" : "false"
                },
                ["features"] = new Dictionary<string, object>
                {
                    ["recording"] = false,
                    ["livestreaming"] = false,
                    ["transcription"] = false,
                    ["outbound-call"] = false,
                    ["sip-outbound-call"] = false
                }
            }
        };

        var signingInput =
            $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";

        byte[] signature;
        lock (_signLock)
        {
            signature = _rsa.SignData(
                Encoding.ASCII.GetBytes(signingInput),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string? ResolvePem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().Trim('"').Replace("\\n", "\n");
        if (text.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            return text;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(text));
            return decoded.Contains("-----BEGIN", StringComparison.Ordinal) ? decoded : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
