using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Api.Modules.Identity.Controllers.Auth;
using SeniorCare.Api.Modules.Identity.Controllers.Users;
using SeniorCare.Api.Modules.Identity.Repositories.Auth;
using SeniorCare.Api.Modules.Identity.Security;
using SeniorCare.Api.Modules.Identity.Services.Auth;
using SeniorCare.Api.Modules.Identity.Services.Users;
using SeniorCare.Shared.Data;

namespace SeniorCare.Api.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services)
    {
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<RolePanelService>();

        services.AddSingleton<IdentityRepository>();
        services.AddSingleton<IIdentityRepository>(provider =>
            provider.GetRequiredService<IdentityRepository>());

        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<UserAdministrationService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("login", context =>
            {
                var forwardedIp =
                    context.Request.Headers["X-Real-IP"].ToString();

                var clientKey = string.IsNullOrWhiteSpace(forwardedIp)
                    ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                    : forwardedIp;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    public static async Task InitializeIdentityModuleAsync(
        this WebApplication app)
    {
        var repository =
            app.Services.GetRequiredService<IdentityRepository>();

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentityDatabaseInitialization");

        await DatabaseRetry.ExecuteAsync(
            () => repository.InitializeAsync(
                app.Lifetime.ApplicationStopping),
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static IEndpointRouteBuilder MapIdentityModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthController();
        endpoints.MapUserController();

        return endpoints;
    }
}