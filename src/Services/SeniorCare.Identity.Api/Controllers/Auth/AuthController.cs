using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Identity.Api.Domain;
using SeniorCare.Identity.Api.Services.Auth;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Identity.Api.Controllers.Auth;

public static class AuthController
{
    public static IEndpointRouteBuilder MapAuthController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity")
            .WithTags("Autenticación");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login");

        group.MapGet("/me", GetSession)
            .RequireAuthorization();

        group.MapGet("/panel", GetPanel)
            .RequireAuthorization();

        group.MapPost("/logout", () => Results.NoContent())
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var result = await authentication.LoginAsync(request, cancellationToken);
        return result.IsSuccess && result.Login is not null
            ? Results.Ok(result.Login)
            : Results.Json(
                new { message = result.Message },
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult GetSession(
        ClaimsPrincipal principal,
        RolePanelService panels)
    {
        var role = principal.GetRole();
        if (string.IsNullOrWhiteSpace(role) || !SeniorCareRoles.IsSupported(role))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            id = principal.GetUserId(),
            username = principal.GetUsername(),
            displayName = principal.Identity?.Name,
            role,
            residentId = principal.GetResidentId(),
            panelPath = panels.Resolve(role)
        });
    }

    private static IResult GetPanel(
        ClaimsPrincipal principal,
        RolePanelService panels)
    {
        var role = principal.GetRole();
        return string.IsNullOrWhiteSpace(role) || !SeniorCareRoles.IsSupported(role)
            ? Results.Unauthorized()
            : Results.Ok(new { role, panelPath = panels.Resolve(role) });
    }
}
