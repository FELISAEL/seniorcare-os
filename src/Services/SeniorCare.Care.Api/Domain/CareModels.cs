namespace SeniorCare.Care.Api.Domain;

public sealed record Resident(
    Guid Id,
    string Username,
    string FullName,
    DateOnly? BirthDate,
    string EmergencyContactName,
    string EmergencyContactPhone,
    DateTimeOffset CreatedAt);

public sealed record CreateResidentRequest(
    string Username,
    string FullName,
    DateOnly? BirthDate,
    string EmergencyContactName,
    string EmergencyContactPhone);

public sealed record MedicationScheduleView(
    Guid ScheduleId,
    Guid MedicationId,
    string MedicationName,
    string Dosage,
    string Instructions,
    TimeOnly Time,
    string Status,
    DateTimeOffset? ConfirmedAt);

public sealed record CreateMedicationRequest(
    string Name,
    string Dosage,
    string Instructions,
    TimeOnly Time);

public sealed record DoseConfirmation(
    Guid ScheduleId,
    Guid ResidentId,
    DateOnly ScheduledDate,
    string Status,
    DateTimeOffset ConfirmedAt);

