using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using System.ComponentModel;
using ServiceBusSearch.Services;
using Newtonsoft.Json;

namespace ServiceBusSearch.Commands;

public class Peek : AsyncCommand<Peek.Settings>
{
    private readonly ISBClient _sbClient;

    public Peek(ISBClient sbClient)
    {
        _sbClient = sbClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--queue <QUEUE>")]
        [Description("The name of the service bus queue")]
        public string Queue { get; set; } = String.Empty;

        [CommandOption("--max <MAX>")]
        [Description("The maximum number of messages to peek from the queue")]
        public int Max { get; set; } = 100;

        [CommandOption("--json <JSON>")]
        [Description("Print the messages in json")]
        public bool Json { get; set; } = false;

        [CommandOption("--table <TABLE>")]
        [Description("Print the messages as a table")]
        public bool Table { get; set; } = false;
    }

    public async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var msgs = await _sbClient.PeekDLQ(settings.Queue, settings.Max);

        AnsiConsole.MarkupLine($"Reading DLQ of: [bold blue]{settings.Queue}[/]!");

        // TODO: Move these to functions
        if (settings.Json)
        {
            var json = JsonConvert.SerializeObject(
    msgs,
    Formatting.Indented);

            var jsonText = new JsonText(json)
                .MemberColor(Color.Purple)
                .NumberColor(Color.DarkOrange)
                .StringColor(Color.Green);
            AnsiConsole.Write(jsonText);
        }

        if (settings.Table)
        {
            var table = new Table();
            table.AddColumn("Id");
            table.AddColumn("Type");
            foreach (var msg in msgs)
            {
                table.AddRow(msg.Id, msg.Type);
            }
            AnsiConsole.Write(table);
        }

        return 0;
    }
}
