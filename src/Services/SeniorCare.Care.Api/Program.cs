using SeniorCare.Care.Api.Controllers.Medications;
using SeniorCare.Care.Api.Controllers.Residents;
using SeniorCare.Care.Api.Repositories;
using SeniorCare.Care.Api.Services.Medications;
using SeniorCare.Care.Api.Services.Residents;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Data;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-care-api");
builder.Services.AddSeniorCareAuthentication(builder.Configuration);
builder.Services.AddSingleton<CareRepository>();
builder.Services.AddSingleton<ICareRepository>(provider =>
    provider.GetRequiredService<CareRepository>());
builder.Services.AddSingleton<ResidentAccessService>();
builder.Services.AddSingleton<ResidentService>();
builder.Services.AddSingleton<MedicationService>();

var app = builder.Build();
app.UseSeniorCareServiceDefaults();

var repository = app.Services.GetRequiredService<CareRepository>();
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("DatabaseInitialization");
await DatabaseRetry.ExecuteAsync(
    () => repository.InitializeAsync(app.Lifetime.ApplicationStopping),
    logger,
    app.Lifetime.ApplicationStopping);

app.MapResidentsController();
app.MapMedicationsController();

app.Run();
