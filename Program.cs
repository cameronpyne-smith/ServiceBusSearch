using Spectre.Console.Cli;
using ServiceBusSearch.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusSearch.Services;

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

        services.AddSingleton<ISBClient, SBClient>();

        var registrar = new TypeRegistrar(services);

        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("servicebus-search");

            config.AddCommand<Peek>("peek")
                  .WithDescription("Peek the messages in the queue");

            config.AddCommand<Stats>("stats");
        });

        return app.Run(args);
    }
}
