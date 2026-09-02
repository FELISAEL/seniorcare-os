namespace SeniorCare.Api.App.Models.Communication;

public sealed record CreateVideoRoomRequest(
    Guid ResidentId,
    string ResidentName);

public sealed record VideoRoom(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string RoomCode,
    string RoomUrl,
    string Status,
    DateTimeOffset CreatedAt);
