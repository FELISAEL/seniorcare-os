using SeniorCare.Api.Modules.Identity.Domain;
using SeniorCare.Api.Modules.Identity.Services.Users;
using SeniorCare.Shared.Auth;

namespace SeniorCare.Api.Modules.Identity.Controllers.Users;

public static class UserController
{
    public static IEndpointRouteBuilder MapUserController(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/identity/users")
            .WithTags("Usuarios")
            .RequireAuthorization(SeniorCarePolicies.AdminOnly);

        group.MapGet("", async (
            UserAdministrationService users,
            CancellationToken cancellationToken) =>
            Results.Ok(await users.ListAsync(cancellationToken)));

        group.MapPost("", async (
            CreateUserRequest request,
            UserAdministrationService users,
            CancellationToken cancellationToken) =>
        {
            var result = await users.CreateAsync(request, cancellationToken);
            return result.IsSuccess && result.User is not null
                ? Results.Created($"/api/identity/users/{result.User.Id}", result.User)
                : Results.BadRequest(new { message = result.Message });
        });

        return endpoints;
    }
}

