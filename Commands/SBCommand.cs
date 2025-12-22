using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Services;

namespace ServiceBusSearch.Commands;

public class SBCommand : AsyncCommand<SBCommand.Settings>
{
    private readonly IServiceBus _serviceBus;

    public SBCommand(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _serviceBus.GetDeadLetter(settings.Queue);

        AnsiConsole.MarkupLine($"Reading DLQ of: [bold blue]{settings.Queue}[/]!");

        var table = new Table();
        table.AddColumn("CorrelationId");
        table.AddColumn("Type");
        table.AddRow("123", "TODO");

        AnsiConsole.Write(table);

        return 0;
    }
}
