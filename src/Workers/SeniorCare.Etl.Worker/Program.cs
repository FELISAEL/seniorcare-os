using SeniorCare.Etl.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();

