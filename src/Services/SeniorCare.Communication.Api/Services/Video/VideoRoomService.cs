using System.Collections.Concurrent;
using System.Security.Cryptography;
using SeniorCare.Communication.Api.Domain;

namespace SeniorCare.Communication.Api.Services.Video;

public sealed class VideoRoomService
{
    private readonly ConcurrentDictionary<Guid, VideoRoom> _rooms = new();
    private readonly string _providerBaseUrl;

    public VideoRoomService(IConfiguration configuration)
    {
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
