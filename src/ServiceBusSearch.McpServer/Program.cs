using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using ServiceBusSearch.Core;
using ServiceBusSearch.Core.Services;
using ServiceBusSearch.McpServer.McpTools;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

var settings = new AppSettings();
builder.Configuration.Bind(settings);

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<ServiceBusClient>(_ => new ServiceBusClient(settings.ServiceBusConnectionString));
builder.Services.AddSingleton<ISBClient, SBClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ServiceBusTools>();

var host = builder.Build();
await host.RunAsync();
