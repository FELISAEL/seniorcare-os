namespace SeniorCare.Api.App.Models.Emergency;

/// <summary>
/// CreatedAt es la hora real del evento (la informada por el dispositivo, si es creíble);
/// ReceivedAt es cuándo llegó al servidor. Difieren cuando el aviso esperó en la cola del panel.
/// </summary>
public sealed record Alert(
    Guid Id,
    Guid ResidentId,
    string ResidentName,
    string Type,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ReceivedAt = null,
    Guid? ClientRequestId = null)
{
    public static readonly TimeSpan DelayThreshold = TimeSpan.FromMinutes(2);

    public bool Delayed =>
        ReceivedAt is { } received && received - CreatedAt > DelayThreshold;
}

/// <summary>
/// ClientRequestId identifica el aviso en el dispositivo: los reintentos con el mismo
/// identificador no crean emergencias duplicadas. OccurredAt es la hora en que se pulsó.
/// </summary>
public sealed record CreateAlertRequest(
    Guid ResidentId,
    string ResidentName,
    string Type,
    string Message,
    Guid? ClientRequestId = null,
    DateTimeOffset? OccurredAt = null);

/// <summary>Resultado de registrar un aviso: Created es false si ya existía (reintento).</summary>
public sealed record AlertCreationResult(Alert Alert, bool Created);

public sealed record UpdateAlertRequest(string Status);
