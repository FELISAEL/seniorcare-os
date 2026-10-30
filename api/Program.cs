using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.Config;

var builder = WebApplication.CreateBuilder(args);

InsecureSecretGuard.EnsureSafe(builder.Configuration);

builder.AddSeniorCareServiceDefaults("seniorcare-api");

builder.Services.AddSeniorCareAuthentication(
    builder.Configuration);

builder.Services.AddIdentityModule();
builder.Services.AddCareModule();
builder.Services.AddEmergencyModule();
builder.Services.AddCommunicationModule();
builder.Services.AddNotificationsModule();
builder.Services.AddAnalyticsModule();

var app = builder.Build();

app.UseSeniorCareServiceDefaults();
app.UseRateLimiter();

if (!app.Environment.IsEnvironment("Testing"))
{
    await app.InitializeIdentityModuleAsync();
    await app.InitializeCareModuleAsync();
    await app.InitializeEmergencyModuleAsync();
    await app.InitializeAnalyticsModuleAsync();
}

app.MapIdentityModule();
app.MapCareModule();
app.MapEmergencyModule();
app.MapCommunicationModule();
app.MapNotificationsModule();
app.MapAnalyticsModule();

app.Run();
public partial class Program { }


