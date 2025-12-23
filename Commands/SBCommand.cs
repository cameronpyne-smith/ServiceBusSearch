using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using ServiceBusSearch.Services;

namespace ServiceBusSearch.Commands;

public class Peek : AsyncCommand<Peek.Settings>
{
    private readonly IServiceBus _serviceBus;

    public Peek(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--Max <MAX>")]
        [Description("The maximum number of messages to peek from the queue")]
        public int Max { get; set; } = 100;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var msgs = await _serviceBus.PeekDLQ(settings.Queue, settings.Max);

        AnsiConsole.MarkupLine($"Reading DLQ of: [bold blue]{settings.Queue}[/]!");

        var table = new Table();
        table.AddColumn("Id");
        table.AddColumn("Type");
        foreach (var msg in msgs)
        {
            table.AddRow(msg.Id, msg.Type);
        }

        AnsiConsole.Write(table);



        return 0;
    }
}
