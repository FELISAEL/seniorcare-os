using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeniorCare.Api.App.Models.Communication;
using SeniorCare.Api.App.Services.Communication;
using Microsoft.Extensions.Configuration;

namespace SeniorCare.Api.Tests.Services.Communication;

public sealed class JaasTokenServiceTests
{
    private const string AppId = "vpaas-magic-cookie-test";
    private const string KeyId = AppId + "/abc123";

    private static (JaasTokenService Service, RSA Rsa) Create()
    {
        var rsa = RSA.Create(2048);
        var service = new JaasTokenService(null, KeyId, rsa.ExportPkcs8PrivateKeyPem());
        return (service, rsa);
    }

    [Fact]
    public void NotConfigured_WithoutKey()
    {
        Assert.False(new JaasTokenService(null, null, null).IsConfigured);
        Assert.False(new JaasTokenService(null, KeyId, "no es una llave").IsConfigured);
    }

    [Fact]
    public void Token_HasValidSignatureAndExpectedClaims()
    {
        var (service, rsa) = Create();
        var now = DateTimeOffset.UtcNow;

        var token = service.BuildToken("sala-1", "María", true, TimeSpan.FromHours(1), now);
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        Assert.True(rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Decode(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));

        using var header = JsonDocument.Parse(Decode(parts[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(KeyId, header.RootElement.GetProperty("kid").GetString());

        using var payload = JsonDocument.Parse(Decode(parts[1]));
        var root = payload.RootElement;
        Assert.Equal("jitsi", root.GetProperty("aud").GetString());
        Assert.Equal("chat", root.GetProperty("iss").GetString());
        Assert.Equal(AppId, root.GetProperty("sub").GetString());
        Assert.Equal("sala-1", root.GetProperty("room").GetString());
        Assert.Equal(now.AddHours(1).ToUnixTimeSeconds(), root.GetProperty("exp").GetInt64());
        var user = root.GetProperty("context").GetProperty("user");
        Assert.Equal("María", user.GetProperty("name").GetString());
        Assert.Equal("true", user.GetProperty("moderator").GetString());
    }

    [Fact]
    public void PrivateKey_AcceptsBase64AndEscapedNewlines()
    {
        var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
        var escaped = pem.Replace("\r", "").Replace("\n", "\\n");

        Assert.True(new JaasTokenService(null, KeyId, base64).IsConfigured);
        Assert.True(new JaasTokenService(null, KeyId, escaped).IsConfigured);
    }

    [Fact]
    public async Task CreateOrGetAsync_UsesJaasAndForViewerAddsToken()
    {
        var (jaas, _) = Create();
        var service = new VideoRoomService(
            new ConfigurationBuilder().Build(), jaas: jaas);

        var room = await service.CreateOrGetAsync(new CreateVideoRoomRequest(Guid.NewGuid(), "María"));

        Assert.Equal("jaas", room.Provider);
        Assert.True(room.FamilyJoined);
        Assert.Equal($"https://8x8.vc/{AppId}/{room.RoomCode}", room.RoomUrl);

        var personal = service.ForViewer(room, "Ana");
        Assert.StartsWith($"https://8x8.vc/{AppId}/{room.RoomCode}?jwt=", personal.RoomUrl);
        Assert.Equal($"https://8x8.vc/{AppId}/{room.RoomCode}", service.FamilyJoinUrl(room));
        Assert.Equal(AppId, personal.JaasAppId);
        Assert.False(string.IsNullOrEmpty(personal.JaasToken));
    }

    [Fact]
    public void Notification_CooldownBlocksUntilFiveMinutesPass()
    {
        var service = new VideoRoomService(new ConfigurationBuilder().Build());
        var roomId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;

        Assert.True(service.TryReserveNotification(roomId, out var first, start));
        Assert.Equal(300, first);

        Assert.False(service.TryReserveNotification(roomId, out var retry, start.AddMinutes(2)));
        Assert.Equal(180, retry);
        Assert.Equal(180, service.NotificationRetryAfterSeconds(roomId, start.AddMinutes(2)));

        Assert.True(service.TryReserveNotification(roomId, out _, start.AddMinutes(5)));
        Assert.Equal(0, service.NotificationRetryAfterSeconds(Guid.NewGuid()));
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
