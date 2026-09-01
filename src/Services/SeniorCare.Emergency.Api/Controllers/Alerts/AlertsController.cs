using System.Security.Claims;
using SeniorCare.Emergency.Api.Domain;
using SeniorCare.Emergency.Api.Services.Alerts;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Emergency.Api.Controllers.Alerts;

public static class AlertsController
{
    public static IEndpointRouteBuilder MapAlertsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/emergencies")
            .WithTags("Emergencias y asistencia");

        group.MapPost("/alerts", CreateAlertAsync)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        group.MapGet("/alerts/active", async (
            AlertService alerts,
            CancellationToken cancellationToken) =>
            Results.Ok(await alerts.ListActiveAsync(cancellationToken)))
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet("/residents/{residentId:guid}/alerts", ListForResidentAsync)
            .RequireAuthorization();

        group.MapPatch("/alerts/{alertId:guid}", UpdateAlertAsync)
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        return endpoints;
    }

    private static async Task<IResult> CreateAlertAsync(
        CreateAlertRequest request,
        ClaimsPrincipal principal,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        if (request.ResidentId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ResidentName)
            || !alerts.IsValidType(request.Type))
        {
            return Results.BadRequest(new
            {
                message = "La solicitud de alerta no es válida."
            });
        }

        if (!principal.CanAccessResident(request.ResidentId))
        {
            return Results.Forbid();
        }

        var created = await alerts.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/emergencies/alerts/{created.Id}", created);
    }

    private static async Task<IResult> ListForResidentAsync(
        Guid residentId,
        ClaimsPrincipal principal,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        if (!principal.CanAccessResident(residentId))
        {
            return Results.Forbid();
        }

        return Results.Ok(await alerts.ListForResidentAsync(
            residentId,
            cancellationToken));
    }

    private static async Task<IResult> UpdateAlertAsync(
        Guid alertId,
        UpdateAlertRequest request,
        ClaimsPrincipal principal,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        if (!alerts.IsValidStatus(request.Status))
        {
            return Results.BadRequest(new
            {
                message = "El estado debe ser acknowledged o resolved."
            });
        }

        var updatedBy = principal.GetUsername()
            ?? principal.Identity?.Name
            ?? "operador";

        var updated = await alerts.UpdateStatusAsync(
            alertId,
            request.Status,
            updatedBy,
            cancellationToken);

        return updated is null
            ? Results.NotFound(new { message = "No se encontró la alerta." })
            : Results.Ok(updated);
    }
}
