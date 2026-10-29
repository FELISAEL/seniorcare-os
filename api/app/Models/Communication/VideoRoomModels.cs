namespace SeniorCare.Api.App.Models.Communication;

public sealed record CreateVideoRoomRequest(
    Guid ResidentId,
    string ResidentName,
    string? Provider = null);

public sealed record VideoRoom(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string RoomCode,
    string RoomUrl,
    string Status,
    DateTimeOffset CreatedAt,
    string Provider = "jitsi",
    string? ProviderSpace = null,
    bool FamilyJoined = true,
    string? JaasAppId = null,
    string? JaasToken = null,
    int NotifyRetryAfterSeconds = 0);
