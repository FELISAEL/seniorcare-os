namespace SeniorCare.Api.App.Models.Identity;

public sealed record UserAccount(
    Guid Id,
    string Username,
    string DisplayName,
    string PasswordHash,
    string Role,
    Guid? ResidentId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record PublicUser(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    Guid? ResidentId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record LoginRequest(
    string Username,
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    PublicUser User,
    string PanelPath);

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string Password,
    string Role,
    Guid? ResidentId);

public sealed record AuthenticationResult(
    bool IsSuccess,
    string Message,
    LoginResponse? Login);

public sealed record UserCreationResult(
    bool IsSuccess,
    string Message,
    PublicUser? User);
