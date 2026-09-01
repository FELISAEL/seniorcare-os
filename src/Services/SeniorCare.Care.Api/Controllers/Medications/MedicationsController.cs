using System.Security.Claims;
using SeniorCare.Care.Api.Domain;
using SeniorCare.Care.Api.Services.Medications;
using SeniorCare.Care.Api.Services.Residents;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Care.Api.Controllers.Medications;

public static class MedicationsController
{
    public static IEndpointRouteBuilder MapMedicationsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/care/residents/{residentId:guid}/medications")
            .WithTags("Medicamentos");

        group.MapGet("/today", GetTodayAsync)
            .RequireAuthorization();

        group.MapPost("", CreateMedicationAsync)
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapPost("/{scheduleId:guid}/confirm", ConfirmDoseAsync)
            .RequireAuthorization(SeniorCarePolicies.ResidentOrCareTeam);

        return endpoints;
    }

    private static async Task<IResult> GetTodayAsync(
        Guid residentId,
        DateOnly? date,
        ClaimsPrincipal principal,
        ResidentAccessService access,
        MedicationService medications,
        CancellationToken cancellationToken)
    {
        if (!access.CanRead(principal, residentId))
        {
            return Results.Forbid();
        }

        var localDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var schedules = await medications.GetTodayAsync(
            residentId,
            localDate,
            cancellationToken);
        return Results.Ok(schedules);
    }

    private static async Task<IResult> CreateMedicationAsync(
        Guid residentId,
        CreateMedicationRequest request,
        ClaimsPrincipal principal,
        ResidentAccessService access,
        ResidentService residents,
        MedicationService medications,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageCare(principal, residentId))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Dosage)
            || string.IsNullOrWhiteSpace(request.Instructions))
        {
            return Results.BadRequest(new
            {
                message = "Completá los datos del medicamento."
            });
        }

        var resident = await residents.FindByIdAsync(residentId, cancellationToken);
        if (resident is null)
        {
            return Results.NotFound(new
            {
                message = "No se encontró la persona adulta mayor."
            });
        }

        var created = await medications.CreateAsync(
            residentId,
            request,
            cancellationToken);

        return Results.Created(
            $"/api/care/residents/{residentId}/medications/today",
            created);
    }

    private static async Task<IResult> ConfirmDoseAsync(
        Guid residentId,
        Guid scheduleId,
        DateOnly? date,
        ClaimsPrincipal principal,
        ResidentAccessService access,
        MedicationService medications,
        CancellationToken cancellationToken)
    {
        if (!access.CanConfirmDose(principal, residentId))
        {
            return Results.Forbid();
        }

        var localDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var confirmation = await medications.ConfirmDoseAsync(
            residentId,
            scheduleId,
            localDate,
            cancellationToken);

        return confirmation is null
            ? Results.NotFound(new { message = "No se encontró el horario indicado." })
            : Results.Ok(confirmation);
    }
}
