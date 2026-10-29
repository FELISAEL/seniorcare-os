namespace SeniorCare.Api.App.Models.Care;

public sealed record Resident(
    Guid Id,
    string Username,
    string FullName,
    DateOnly? BirthDate,
    string EmergencyContactName,
    string EmergencyContactPhone,
    DateTimeOffset CreatedAt,
    string? FamilyEmail = null);

public sealed record CreateResidentRequest(
    string Username,
    string FullName,
    DateOnly? BirthDate,
    string EmergencyContactName,
    string EmergencyContactPhone,
    string? FamilyEmail = null);

public sealed record UpdateFamilyEmailRequest(string? FamilyEmail);

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

