using System.Security.Claims;
using Npgsql;
using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Services.Care;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Controllers.Care;

public static class ResidentsController
{
    public static IEndpointRouteBuilder MapResidentsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/care")
            .WithTags("Personas adultas mayores");

        group.MapGet("/residents", async (
            ResidentService residents,
            CancellationToken cancellationToken) =>
            Results.Ok(await residents.ListAsync(cancellationToken)))
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet("/me/residents", async (
            ClaimsPrincipal principal,
            ResidentService residents,
            CancellationToken cancellationToken) =>
            Results.Ok(await residents.ListForCurrentUserAsync(
                principal,
                cancellationToken)))
            .RequireAuthorization();

        group.MapGet("/residents/by-username/{username}", async (
            string username,
            ResidentService residents,
            CancellationToken cancellationToken) =>
        {
            var resident = await residents.FindByUsernameAsync(username, cancellationToken);
            return resident is null
                ? Results.NotFound(new { message = "No se encontró la persona adulta mayor." })
                : Results.Ok(resident);
        }).RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet("/residents/{residentId:guid}", async (
            Guid residentId,
            ClaimsPrincipal principal,
            ResidentService residents,
            ResidentAccessService access,
            CancellationToken cancellationToken) =>
        {
            if (!access.CanRead(principal, residentId))
            {
                return Results.Forbid();
            }

            var resident = await residents.FindByIdAsync(residentId, cancellationToken);
            return resident is null
                ? Results.NotFound(new { message = "No se encontró la persona adulta mayor." })
                : Results.Ok(resident);
        }).RequireAuthorization();

        group.MapPost("/residents", CreateResidentAsync)
            .RequireAuthorization(SeniorCarePolicies.AdminOnly);

        return endpoints;
    }

    private static async Task<IResult> CreateResidentAsync(
        CreateResidentRequest request,
        ResidentService residents,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.EmergencyContactName)
            || string.IsNullOrWhiteSpace(request.EmergencyContactPhone))
        {
            return Results.BadRequest(new
            {
                message = "Completá todos los datos requeridos."
            });
        }

        try
        {
            var created = await residents.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/care/residents/{created.Id}", created);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new
            {
                message = "El usuario de la persona adulta mayor ya existe."
            });
        }
    }
}
