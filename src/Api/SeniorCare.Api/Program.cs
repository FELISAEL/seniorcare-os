using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Api.Modules.Identity;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-api");

builder.Services.AddSeniorCareAuthentication(
    builder.Configuration);

builder.Services.AddIdentityModule();

var app = builder.Build();

app.UseSeniorCareServiceDefaults();
app.UseRateLimiter();

await app.InitializeIdentityModuleAsync();

app.MapIdentityModule();

app.Run();