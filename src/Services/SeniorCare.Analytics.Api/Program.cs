using SeniorCare.Analytics.Api.Controllers.Metrics;
using SeniorCare.Analytics.Api.Repositories;
using SeniorCare.Analytics.Api.Services.Metrics;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Data;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-analytics-api");
builder.Services.AddSeniorCareAuthentication(builder.Configuration);
builder.Services.AddSingleton<AnalyticsRepository>();
builder.Services.AddSingleton<IAnalyticsRepository>(provider =>
    provider.GetRequiredService<AnalyticsRepository>());
builder.Services.AddSingleton<AnalyticsService>();

var app = builder.Build();
app.UseSeniorCareServiceDefaults();

var repository = app.Services.GetRequiredService<AnalyticsRepository>();
var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("DatabaseInitialization");
await DatabaseRetry.ExecuteAsync(
    () => repository.InitializeAsync(app.Lifetime.ApplicationStopping),
    logger,
    app.Lifetime.ApplicationStopping);

app.MapMetricsController();

app.Run();
