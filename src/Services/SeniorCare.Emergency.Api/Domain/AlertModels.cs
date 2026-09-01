namespace SeniorCare.Emergency.Api.Domain;

public sealed record Alert(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string Type,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record CreateAlertRequest(
    Guid ResidentId,
    string ResidentName,
    string Type,
    string Message);

public sealed record UpdateAlertRequest(string Status);

