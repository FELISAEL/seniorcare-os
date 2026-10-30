using SeniorCare.Etl.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(EtlOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMetricsStore>(services =>
    PostgresMetricsStore.FromConfiguration(
        builder.Configuration,
        services.GetRequiredService<EtlOptions>()));
builder.Services.AddSingleton<MetricsProcessor>();
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();
