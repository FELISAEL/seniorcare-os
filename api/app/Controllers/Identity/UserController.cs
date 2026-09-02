using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Services.Identity;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Controllers.Identity;

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
