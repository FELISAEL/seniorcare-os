using Microsoft.Maui.Graphics;

namespace SeniorCare.Mobile.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record PublicUser(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    Guid? ResidentId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    PublicUser User,
    string PanelPath);

public sealed record AlertItem(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string Type,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy)
{
    public string TypeLabel => Type == "emergency" ? "EMERGENCIA SOS" : "ASISTENCIA";
    public Color TypeColor => Type == "emergency"
        ? Color.FromArgb("#C82D34")
        : Color.FromArgb("#A76305");
    public string CreatedLabel =>
        CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed record VideoRoom(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string RoomCode,
    string RoomUrl,
    string Status,
    DateTimeOffset CreatedAt);
