using Microsoft.AspNetCore.RateLimiting;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.Config;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-api");

builder.Services.AddSeniorCareAuthentication(
    builder.Configuration);

builder.Services.AddIdentityModule();
builder.Services.AddCareModule();
builder.Services.AddEmergencyModule();
builder.Services.AddCommunicationModule();
builder.Services.AddAnalyticsModule();

var app = builder.Build();

app.UseSeniorCareServiceDefaults();
app.UseRateLimiter();

await app.InitializeIdentityModuleAsync();
await app.InitializeCareModuleAsync();
await app.InitializeEmergencyModuleAsync();
await app.InitializeAnalyticsModuleAsync();

app.MapIdentityModule();
app.MapCareModule();
app.MapEmergencyModule();
app.MapCommunicationModule();
app.MapAnalyticsModule();

app.Run();