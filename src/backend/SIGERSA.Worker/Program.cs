using System.Globalization;
using Serilog;
using SIGERSA.Application.DependencyInjection;
using SIGERSA.Infrastructure;
using SIGERSA.Infrastructure.Configuration;
using SIGERSA.Worker;

LocalEnvironmentFile.LoadIfEnabled();
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
