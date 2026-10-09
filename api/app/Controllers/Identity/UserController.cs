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

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            UserAdministrationService users,
            CancellationToken cancellationToken) =>
        {
            var result = await users.UpdateAsync(id, request, cancellationToken);
            return result.IsSuccess && result.User is not null
                ? Results.Ok(result.User)
                : Results.BadRequest(new { message = result.Message });
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            UserAdministrationService users,
            CancellationToken cancellationToken) =>
        {
            var result = await users.DeleteAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { message = result.Message });
        });

        return endpoints;
    }
}