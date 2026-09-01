using SeniorCare.Emergency.Api.Controllers.Alerts;
using SeniorCare.Emergency.Api.Repositories;
using SeniorCare.Emergency.Api.Services.Alerts;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Data;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-emergency-api");
builder.Services.AddSeniorCareAuthentication(builder.Configuration);
builder.Services.AddSingleton<EmergencyRepository>();
builder.Services.AddSingleton<IEmergencyRepository>(provider =>
    provider.GetRequiredService<EmergencyRepository>());
builder.Services.AddSingleton<AlertService>();

var app = builder.Build();
app.UseSeniorCareServiceDefaults();

var repository = app.Services.GetRequiredService<EmergencyRepository>();
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("DatabaseInitialization");
await DatabaseRetry.ExecuteAsync(
    () => repository.InitializeAsync(app.Lifetime.ApplicationStopping),
    logger,
    app.Lifetime.ApplicationStopping);

app.MapAlertsController();

app.Run();
