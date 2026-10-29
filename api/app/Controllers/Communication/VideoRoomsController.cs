using System.Security.Claims;
using SeniorCare.Api.App.Models.Communication;
using SeniorCare.Api.App.Services.Communication;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Services.Notifications;

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
            ClaimsPrincipal principal,
            VideoRoomService rooms) => Results.Ok(
                rooms.ListActive().Select(room => rooms.ForViewer(room, ViewerName(principal)))))
            .RequireAuthorization(SeniorCarePolicies.CareTeam);

        group.MapGet(
            "/residents/{residentId:guid}/video-rooms/active",
            ListForResident)
            .RequireAuthorization();

        group.MapGet("/video-rooms/{roomId:guid}", GetRoom)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        group.MapPost("/video-rooms/{roomId:guid}/notify-family", NotifyFamily)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        group.MapPatch("/video-rooms/{roomId:guid}/close", CloseRoom)
            .RequireAuthorization(SeniorCarePolicies.ResidentFamilyOrCareTeam);

        return endpoints;
    }

    private static async Task<IResult> CreateRoom(
        CreateVideoRoomRequest request,
        ClaimsPrincipal principal,
        VideoRoomService rooms,
        ICareRepository care,
        FamilyNotificationService notifier,
        CancellationToken cancellationToken)
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

        // Cuando el propio residente llama y tiene familiar con correo, se avisa
        // al familiar por correo en cuanto empieza la videollamada. Se reconoce al
        // residente por su rol (o por su usuario). Si ya se avisó hace menos de
        // 5 minutos para esta sala, no se repite el correo.
        var resident = await care.FindResidentByIdAsync(request.ResidentId, cancellationToken);
        var startedByResident = principal.IsInRole(SeniorCareRoles.Resident)
            || (resident is not null
                && string.Equals(
                    principal.Identity?.Name,
                    resident.Username,
                    StringComparison.OrdinalIgnoreCase));

        var wait = startedByResident
            && resident is not null
            && !string.IsNullOrWhiteSpace(resident.FamilyEmail)
            && notifier.CanNotify;

        var room = await rooms.CreateOrGetAsync(request, wait, cancellationToken);

        if (wait && rooms.TryReserveNotification(room.Id, out _))
        {
            notifier.VideoCallRequested(
                resident!,
                rooms.FamilyJoinUrl(room),
                room.CreatedAt);
        }

        return Results.Ok(rooms.ForViewer(room, ViewerName(principal)));
    }

    private static async Task<IResult> NotifyFamily(
        Guid roomId,
        ClaimsPrincipal principal,
        VideoRoomService rooms,
        ICareRepository care,
        FamilyNotificationService notifier,
        CancellationToken cancellationToken)
    {
        var room = rooms.Find(roomId);
        if (room is null || room.Status != "active")
        {
            return Results.NotFound(new { message = "No se encontró la videollamada." });
        }

        if (!principal.CanAccessResident(room.ResidentId))
        {
            return Results.Forbid();
        }

        var resident = await care.FindResidentByIdAsync(room.ResidentId, cancellationToken);
        if (resident is null || string.IsNullOrWhiteSpace(resident.FamilyEmail))
        {
            return Results.BadRequest(new
            {
                message = "Esta persona no tiene un correo de familiar registrado."
            });
        }

        if (!notifier.CanNotify)
        {
            return Results.BadRequest(new
            {
                message = "Las notificaciones por correo no están activadas."
            });
        }

        if (!rooms.TryReserveNotification(roomId, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter / 60d));
            return Results.Json(
                new
                {
                    message = $"Podrás reenviar la notificación en {minutes} minuto{(minutes == 1 ? "" : "s")}.",
                    retryAfterSeconds = retryAfter
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        notifier.VideoCallRequested(
            resident,
            rooms.FamilyJoinUrl(room),
            room.CreatedAt);

        return Results.Ok(new
        {
            retryAfterSeconds = retryAfter,
            sentTo = MaskEmail(resident.FamilyEmail)
        });
    }

    // e***@gmail.com: confirma a qué cuenta salió el aviso sin mostrar el correo completo.
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "el correo registrado" : $"{email[0]}***{email[at..]}";
    }

    private static IResult ListForResident(
        Guid residentId,
        ClaimsPrincipal principal,
        VideoRoomService rooms)
    {
        return principal.CanAccessResident(residentId)
            ? Results.Ok(rooms.ListActiveForResident(residentId)
                .Select(room => rooms.ForViewer(room, ViewerName(principal))))
            : Results.Forbid();
    }

    private static async Task<IResult> CloseRoom(
        Guid roomId,
        ClaimsPrincipal principal,
        VideoRoomService rooms,
        CancellationToken cancellationToken)
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

        return Results.Ok(await rooms.CloseAsync(roomId, cancellationToken));
    }

    private static async Task<IResult> GetRoom(
        Guid roomId,
        ClaimsPrincipal principal,
        VideoRoomService rooms,
        CancellationToken cancellationToken)
    {
        var room = await rooms.RefreshAsync(roomId, cancellationToken);
        if (room is null)
        {
            return Results.NotFound(new { message = "No se encontró la videollamada." });
        }

        return principal.CanAccessResident(room.ResidentId)
            ? Results.Ok(rooms.ForViewer(room, ViewerName(principal)))
            : Results.Forbid();
    }

    private static string ViewerName(ClaimsPrincipal principal) =>
        principal.Identity?.Name ?? "SeniorCare";
}
