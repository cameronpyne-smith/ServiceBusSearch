using Spectre.Console.Cli;
using ServiceBusSearch.Cli.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusSearch.Core;
using ServiceBusSearch.Core.Services;
using Azure.Messaging.ServiceBus;

namespace ServiceBusSearch.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        var settings = new AppSettings();
        configuration.Bind(settings);
        services.AddSingleton(settings);

        services.AddSingleton<ServiceBusClient>(_ => new ServiceBusClient(settings.ServiceBusConnectionString));
        services.AddSingleton<ISBClient, SBClient>();

        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("servicebus-search");

            config.AddCommand<Peek>("peek")
                  .WithDescription("Peek the messages in the queue");

            config.AddCommand<Stats>("stats")
                  .WithDescription("Show a bar chart of message types in a queue");

            config.AddCommand<Delete>("delete")
                  .WithDescription("Delete messages from the dead-letter queue by filter");

            config.AddCommand<Undefer>("undefer")
                  .WithDescription("Move all deferred messages in the DLQ back to the main queue");

            config.AddCommand<DeadLetter>("deadletter")
                  .WithDescription("Move all messages in the main queue to the dead-letter queue");

            config.AddCommand<Edit>("edit")
                  .WithDescription("Clone and edit a message from the queue");
        });

        return app.Run(args);
    }
}
