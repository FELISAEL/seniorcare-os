using System.Security.Claims;
using SeniorCare.Api.App.Models.Communication;
using SeniorCare.Api.App.Services.Communication;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Controllers.Communication;

public static class VideoRoomsController
{
    public static IEndpointRouteBuilder MapVideoRoomsController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/communication")
            .WithTags("Videollamadas");

        group.MapPost("/video-rooms", CreateRoom)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        group.MapGet("/video-rooms/active", (
            VideoRoomService rooms) => Results.Ok(rooms.ListActive()))
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet(
            "/residents/{residentId:guid}/video-rooms/active",
            ListForResident)
            .RequireAuthorization();

        group.MapPatch("/video-rooms/{roomId:guid}/close", CloseRoom)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        return endpoints;
    }

    private static IResult CreateRoom(
        CreateVideoRoomRequest request,
        ClaimsPrincipal principal,
        VideoRoomService rooms)
    {
        if (request.ResidentId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ResidentName))
        {
            return Results.BadRequest(new { message = "La persona adulta mayor no es válida." });
        }

        if (!principal.CanAccessResident(request.ResidentId))
        {
            return Results.Forbid();
        }

        return Results.Ok(rooms.CreateOrGet(request));
    }

    private static IResult ListForResident(
        Guid residentId,
        ClaimsPrincipal principal,
        VideoRoomService rooms)
    {
        return principal.CanAccessResident(residentId)
            ? Results.Ok(rooms.ListActiveForResident(residentId))
            : Results.Forbid();
    }

    private static IResult CloseRoom(
        Guid roomId,
        ClaimsPrincipal principal,
        VideoRoomService rooms)
    {
        var room = rooms.Find(roomId);
        if (room is null)
        {
            return Results.NotFound(new { message = "No se encontró la videollamada." });
        }

        if (!principal.CanAccessResident(room.ResidentId))
        {
            return Results.Forbid();
        }

        return Results.Ok(rooms.Close(roomId));
    }
}
