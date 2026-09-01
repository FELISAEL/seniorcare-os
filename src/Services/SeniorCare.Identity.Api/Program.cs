using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Identity.Api.Controllers.Auth;
using SeniorCare.Identity.Api.Controllers.Users;
using SeniorCare.Identity.Api.Repositories.Auth;
using SeniorCare.Identity.Api.Security;
using SeniorCare.Identity.Api.Services.Auth;
using SeniorCare.Identity.Api.Services.Users;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Data;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-identity-api");
builder.Services.AddSeniorCareAuthentication(builder.Configuration);
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RolePanelService>();
builder.Services.AddSingleton<IdentityRepository>();
builder.Services.AddSingleton<IIdentityRepository>(provider =>
    provider.GetRequiredService<IdentityRepository>());
builder.Services.AddSingleton<AuthenticationService>();
builder.Services.AddSingleton<UserAdministrationService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
    {
        var forwardedIp = context.Request.Headers["X-Real-IP"].ToString();
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

var app = builder.Build();
app.UseSeniorCareServiceDefaults();
app.UseRateLimiter();

var repository = app.Services.GetRequiredService<IdentityRepository>();
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("DatabaseInitialization");
await DatabaseRetry.ExecuteAsync(
    () => repository.InitializeAsync(app.Lifetime.ApplicationStopping),
    logger,
    app.Lifetime.ApplicationStopping);

app.MapAuthController();
app.MapUserController();

app.Run();
