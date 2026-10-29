using System.Collections.Concurrent;
using System.Security.Cryptography;
using SeniorCare.Api.App.Models.Communication;

namespace SeniorCare.Api.App.Services.Communication;

public sealed class VideoRoomService
{
    private readonly ConcurrentDictionary<Guid, VideoRoom> _rooms = new();
    private readonly string _providerBaseUrl;

    private readonly GoogleMeetClient? _meet;
    private readonly ILogger<VideoRoomService>? _logger;
    private readonly JaasTokenService? _jaas;

    /// <summary>Tiempo mínimo entre un aviso por correo al familiar y el siguiente.</summary>
    public static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(5);

    private readonly Dictionary<Guid, DateTimeOffset> _notifiedAt = new();
    private readonly object _notifyLock = new();

    public VideoRoomService(
        IConfiguration configuration,
        GoogleMeetClient? meet = null,
        ILogger<VideoRoomService>? logger = null,
        JaasTokenService? jaas = null)
    {
        _meet = meet;
        _logger = logger;
        _jaas = jaas;
        _providerBaseUrl =
            configuration["VIDEO_PROVIDER_BASE_URL"]
            ?? configuration["Video:ProviderBaseUrl"]
            ?? "https://meet.jit.si/";
    }

    public VideoRoom CreateOrGet(CreateVideoRoomRequest request)
    {
        var current = ListActiveForResident(request.ResidentId).FirstOrDefault();
        if (current is not null)
        {
            return current;
        }

        var randomBytes = RandomNumberGenerator.GetBytes(8);
        var suffix = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var roomCode = $"seniorcare-{request.ResidentId:N}-{suffix}";
        var baseUrl = _providerBaseUrl.EndsWith('/')
            ? _providerBaseUrl
            : $"{_providerBaseUrl}/";

        var room = new VideoRoom(
            Guid.NewGuid(),
            request.ResidentId,
            request.ResidentName.Trim(),
            roomCode,
            $"{baseUrl}{roomCode}",
            "active",
            DateTimeOffset.UtcNow);

        _rooms[room.Id] = room;
        return room;
    }

    /// <summary>
    /// Crea la sala con Google Meet si está configurado; si no (o si Google
    /// falla) usa el proveedor Jitsi como respaldo para no dejar sin llamada.
    /// </summary>
    public async Task<VideoRoom> CreateOrGetAsync(
        CreateVideoRoomRequest request,
        bool waitForFamily = false,
        CancellationToken cancellationToken = default)
    {
        var current = ListActiveForResident(request.ResidentId).FirstOrDefault();
        if (current is not null)
        {
            return current;
        }

        // Jitsi alojado por 8x8 (JaaS): la sala se incrusta en la pantalla y
        // todos entran con token de moderador, sin esperar a nadie.
        if (_jaas is { IsConfigured: true })
        {
            var code = $"seniorcare-{request.ResidentId:N}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
            var jaasRoom = new VideoRoom(
                Guid.NewGuid(),
                request.ResidentId,
                request.ResidentName.Trim(),
                code,
                _jaas.RoomUrl(code),
                "active",
                DateTimeOffset.UtcNow,
                "jaas");

            _rooms[jaasRoom.Id] = jaasRoom;
            return jaasRoom;
        }

        // "jitsi" se pide cuando la llamada se muestra dentro de la propia
        // pantalla (Google Meet no se deja incrustar en un iframe).
        var forceJitsi = string.Equals(
            request.Provider, "jitsi", StringComparison.OrdinalIgnoreCase);

        if (!forceJitsi && _meet is { IsConfigured: true })
        {
            try
            {
                var space = await _meet.CreateSpaceAsync(cancellationToken);
                var room = new VideoRoom(
                    Guid.NewGuid(),
                    request.ResidentId,
                    request.ResidentName.Trim(),
                    space.MeetingCode,
                    space.MeetingUri,
                    "active",
                    DateTimeOffset.UtcNow,
                    "google-meet",
                    space.Name,
                    !waitForFamily);

                _rooms[room.Id] = room;
                return room;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "No se pudo crear la reunión de Google Meet; se usa Jitsi.");
            }
        }

        return CreateOrGet(request);
    }

    /// <summary>
    /// Si la sala espera al familiar, consulta a Google Meet si ya hay alguien
    /// conectado (familiar, cuidador o administrador) y la libera.
    /// </summary>
    public async Task<VideoRoom?> RefreshAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = Find(roomId);
        if (room is null
            || room.FamilyJoined
            || room.Status != "active"
            || room.ProviderSpace is null
            || _meet is not { IsConfigured: true })
        {
            return room;
        }

        try
        {
            if (await _meet.HasActiveParticipantsAsync(room.ProviderSpace, cancellationToken))
            {
                room = room with { FamilyJoined = true };
                _rooms[roomId] = room;
            }
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "No se pudo consultar los participantes de Google Meet.");
        }

        return room;
    }

    public async Task<VideoRoom?> CloseAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var closed = Close(roomId);

        if (closed is { Provider: "google-meet", ProviderSpace: not null } && _meet is { IsConfigured: true })
        {
            try
            {
                await _meet.EndActiveConferenceAsync(closed.ProviderSpace, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "No se pudo terminar la reunión de Google Meet.");
            }
        }

        return closed;
    }

    /// <summary>
    /// Devuelve la sala con una dirección personal (con token) para quien la
    /// consulta. Las salas de otros proveedores se devuelven sin cambios.
    /// </summary>
    /// <summary>
    /// Reserva el derecho a avisar al familiar. Devuelve false (y los segundos
    /// que faltan) si el último aviso fue hace menos de 5 minutos.
    /// </summary>
    public bool TryReserveNotification(
        Guid roomId,
        out int retryAfterSeconds,
        DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        lock (_notifyLock)
        {
            if (_notifiedAt.TryGetValue(roomId, out var last)
                && at - last < NotificationCooldown)
            {
                retryAfterSeconds = (int)Math.Ceiling((NotificationCooldown - (at - last)).TotalSeconds);
                return false;
            }

            _notifiedAt[roomId] = at;
            retryAfterSeconds = (int)NotificationCooldown.TotalSeconds;
            return true;
        }
    }

    public int NotificationRetryAfterSeconds(Guid roomId, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        lock (_notifyLock)
        {
            return _notifiedAt.TryGetValue(roomId, out var last) && at - last < NotificationCooldown
                ? (int)Math.Ceiling((NotificationCooldown - (at - last)).TotalSeconds)
                : 0;
        }
    }

    /// <summary>
    /// Dirección para el correo del familiar. En JaaS es la dirección corta de
    /// la sala, sin token: un enlace de más de mil caracteres hace que Gmail
    /// tarde minutos en entregar el correo. Entra como invitado.
    /// </summary>
    public string FamilyJoinUrl(VideoRoom room) =>
        room.Provider == "jaas" && _jaas is { IsConfigured: true }
            ? _jaas.RoomUrl(room.RoomCode)
            : room.RoomUrl;

    public VideoRoom ForViewer(VideoRoom room, string displayName, TimeSpan? lifetime = null)
    {
        room = room with { NotifyRetryAfterSeconds = NotificationRetryAfterSeconds(room.Id) };

        if (room.Provider != "jaas" || _jaas is not { IsConfigured: true })
        {
            return room;
        }

        var token = _jaas.BuildToken(
            room.RoomCode,
            displayName,
            moderator: true,
            lifetime ?? TimeSpan.FromHours(3),
            DateTimeOffset.UtcNow);

        return room with
        {
            RoomUrl = $"{_jaas.RoomUrl(room.RoomCode)}?jwt={token}",
            JaasAppId = _jaas.AppId,
            JaasToken = token
        };
    }

    public IReadOnlyList<VideoRoom> ListActive() =>
        _rooms.Values
            .Where(room => room.Status == "active")
            .OrderByDescending(room => room.CreatedAt)
            .ToArray();

    public IReadOnlyList<VideoRoom> ListActiveForResident(Guid residentId) =>
        _rooms.Values
            .Where(room => room.ResidentId == residentId)
            .Where(room => room.Status == "active")
            .OrderByDescending(room => room.CreatedAt)
            .ToArray();

    public VideoRoom? Find(Guid roomId) =>
        _rooms.TryGetValue(roomId, out var room)
            ? room
            : null;

    public VideoRoom? Close(Guid roomId)
    {
        var room = Find(roomId);
        if (room is null)
        {
            return null;
        }

        var closed = room with { Status = "closed" };
        _rooms[roomId] = closed;
        return closed;
    }
}
