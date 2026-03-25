using Spectre.Console.Cli;
using ServiceBusSearch.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusSearch.Services;
using Azure.Messaging.ServiceBus;

namespace ServiceBusSearch;

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
        services.AddSingleton<AppSettings>(settings);

        services.AddSingleton<ServiceBusClient>(sp => new ServiceBusClient(settings.ServiceBusConnectionString));
        services.AddSingleton<ISBClient, SBClient>();

        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("servicebus-search");

            config.AddCommand<Peek>("peek")
                .WithDescription("Peek the messages in the queue.");
            config.AddCommand<Stats>("stats")
                .WithDescription("Bar chart grouping all messages in a queue by type.");
            config.AddCommand<Delete>("delete")
                .WithDescription("Delete messages by query.");
            config.AddCommand<Undefer>("undefer")
                .WithDescription("Make all deferred messages processable.");
            config.AddCommand<DeadLetter>("deadletter")
                .WithDescription("Move all messages in a queue to the dead letter queue.");
            config.AddCommand<Edit>("edit")
                .WithDescription("Clone and edit a message.");
        });

        return app.Run(args);
    }
}
